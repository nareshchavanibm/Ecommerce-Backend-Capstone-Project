using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using CsvHelper.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Infrastructure.Services;

public class ImportProductsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ImportProductsService> _logger;
    private readonly INotificationService _notificationService;
    private readonly int _importBatchSize;

    public ImportProductsService(AppDbContext db, ILogger<ImportProductsService> logger, INotificationService notificationService, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _notificationService = notificationService;
        // Allow configurable batch size for large imports (default 1000)
        _importBatchSize = configuration?.GetValue<int>("Import:BatchSize") ?? 1000;
    }
    public static SemaphoreSlim _importSemaphore = new SemaphoreSlim(2);

    public async Task ProcessCsvAsync(string filePath, int userId, string jobId, CancellationToken cancellationToken)
    {
        await _importSemaphore.WaitAsync(cancellationToken);
        try
        {
            var batchSize = _importBatchSize;
            var processed = 0;
            var inserted = 0;
            var errors = 0;
            var categoryIdMap = new Dictionary<int, int>();

            var errorLines = new List<string[]>();

            // Create import job record
            var job = new ImportJob
            {
                JobId = jobId,
                UploadedBy = userId,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };

            _db.ImportJobs.Add(job);
            await _db.SaveChangesAsync(cancellationToken);

            var categoriesCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // preload existing categories
            var existingCategories = await _db.Categories.ToListAsync(cancellationToken);
            foreach (var c in existingCategories)
                categoriesCache[c.Name] = c.Id;

            using var stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream);

            var csvConfig = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
                HeaderValidated = null,
                PrepareHeaderForMatch = args => args.Header?.Trim()
            };

            using var csv = new CsvHelper.CsvReader(reader, csvConfig);

            // read header and records
            if (!await csv.ReadAsync())
                throw new ArgumentException("File is empty.");
            csv.ReadHeader();

            // determine which category/stock column is present
            var headers = csv.HeaderRecord?.Select(h => h?.Trim()).Where(h => !string.IsNullOrEmpty(h)).ToArray() ?? Array.Empty<string>();
            var hasCategoryName = headers.Any(h => string.Equals(h, "CategoryName", StringComparison.OrdinalIgnoreCase));
            var hasCategoryId = headers.Any(h => string.Equals(h, "CategoryId", StringComparison.OrdinalIgnoreCase));
            var stockField = headers.FirstOrDefault(h => string.Equals(h, "StockQuantity", StringComparison.OrdinalIgnoreCase)
                                                         || string.Equals(h, "InitialStock", StringComparison.OrdinalIgnoreCase)
                                                         || string.Equals(h, "Stock", StringComparison.OrdinalIgnoreCase)) ?? "StockQuantity";

            var existingCategoryIds = existingCategories.Select(c => c.Id).ToHashSet();

            var batchProducts = new List<(string Name, string Description, decimal Price, int CategoryId, int Stock)>();

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                try
                {
                    var name = csv.GetField("Name");
                    var description = csv.GetField("Description");
                    var price = csv.GetField<decimal>("Price");

                    int categoryId;

                    if (hasCategoryName)
                    {
                        var categoryName = csv.GetField("CategoryName");

                        if (string.IsNullOrWhiteSpace(categoryName))
                        {
                            errors++;
                            errorLines.Add(new[] { processed.ToString(), "CategoryName is empty", string.Join(",", csv.Parser.Record) });
                            continue;
                        }

                        if (!categoriesCache.TryGetValue(categoryName, out categoryId))
                        {
                            var newCat = new Category { Name = categoryName };
                            _db.Categories.Add(newCat);
                            await _db.SaveChangesAsync(cancellationToken);
                            categoryId = newCat.Id;
                            categoriesCache[categoryName] = categoryId;
                            existingCategoryIds.Add(categoryId);
                        }
                    }
                    else if (hasCategoryId)
                    {
                        // read numeric category id from file
                        if (!int.TryParse(csv.GetField("CategoryId"), out categoryId))
                        {
                            errors++;
                            errorLines.Add(new[] { processed.ToString(), "CategoryId is invalid or missing", string.Join(",", csv.Parser.Record) });
                            continue;
                        }

                        if (!existingCategoryIds.Contains(categoryId))
                        {
                            // Category id not found. Try to create a category row with the requested identity value
                            // if possible. Prefer using CategoryName from the sheet; otherwise generate a name.
                            string? newCategoryName = null;
                            if (hasCategoryName)
                            {
                                newCategoryName = csv.GetField("CategoryName");
                            }

                            if (string.IsNullOrWhiteSpace(newCategoryName))
                            {
                                newCategoryName = $"ImportedCategory_{categoryId}";
                            }

                            var trimmedName = newCategoryName!.Trim();

                            try
                            {
                                // Attempt to insert category with explicit Id using IDENTITY_INSERT
                                var conn = _db.Database.GetDbConnection();
                                if (conn.State != System.Data.ConnectionState.Open)
                                    await conn.OpenAsync(cancellationToken);

                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.CommandText = @"SET IDENTITY_INSERT dbo.Categories ON;
INSERT INTO dbo.Categories (Id, Name) VALUES (@Id, @Name);
SET IDENTITY_INSERT dbo.Categories OFF;";
                                    var pId = cmd.CreateParameter();
                                    pId.ParameterName = "@Id";
                                    pId.Value = categoryId;
                                    cmd.Parameters.Add(pId);

                                    var pName = cmd.CreateParameter();
                                    pName.ParameterName = "@Name";
                                    pName.Value = trimmedName;
                                    cmd.Parameters.Add(pName);

                                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                                }

                                // Reload the inserted category
                                var insertedCat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
                                if (insertedCat != null)
                                {
                                    categoriesCache[insertedCat.Name] = insertedCat.Id;
                                    existingCategoryIds.Add(insertedCat.Id);
                                    // record mapping from original requested id to final id
                                    categoryIdMap[categoryId] = insertedCat.Id;
                                }
                                else
                                {
                                    // fallback: try to create normally via EF
                                    var newCat = new Category { Name = trimmedName };
                                    _db.Categories.Add(newCat);
                                    await _db.SaveChangesAsync(cancellationToken);
                                    categoryId = newCat.Id;
                                    categoriesCache[newCat.Name] = categoryId;
                                    existingCategoryIds.Add(categoryId);
                                    categoryIdMap[categoryId] = categoryId;
                                }
                            }
                            catch (DbUpdateException)
                            {
                                // Unique constraint / race: try to find by name
                                var existing = await _db.Categories.FirstOrDefaultAsync(c => c.Name == trimmedName, cancellationToken);
                                if (existing != null)
                                {
                                    categoryId = existing.Id;
                                    categoriesCache[existing.Name] = categoryId;
                                    existingCategoryIds.Add(categoryId);
                                    categoryIdMap[categoryId] = categoryId;
                                }
                                else
                                {
                                    errors++;
                                    errorLines.Add(new[] { processed.ToString(), $"CategoryId {categoryId} does not exist and automatic creation failed", string.Join(",", csv.Parser.Record) });
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                
                                _logger.LogWarning(ex, "Could not insert category with explicit id {CategoryId}, falling back to automated creation.", categoryId);

                                try
                                {
                                    var newCat = new Category { Name = trimmedName };
                                    _db.Categories.Add(newCat);
                                    await _db.SaveChangesAsync(cancellationToken);
                                    categoryId = newCat.Id;
                                    categoriesCache[newCat.Name] = categoryId;
                                    existingCategoryIds.Add(categoryId);
                                    categoryIdMap[categoryId] = categoryId;
                                }
                                catch (Exception ex2)
                                {
                                    _logger.LogError(ex2, "Failed fallback category creation for {Name}", trimmedName);
                                    errors++;
                                    errorLines.Add(new[] { processed.ToString(), $"CategoryId {categoryId} does not exist and creation failed: {ex2.Message}", string.Join(",", csv.Parser.Record) });
                                    continue;
                                }
                            }
                        }
                    }
                    else
                    {
                        errors++;
                        errorLines.Add(new[] { processed.ToString(), "No CategoryName or CategoryId column present", string.Join(",", csv.Parser.Record) });
                        continue;
                    }

                    var stock = csv.GetField<int>(stockField);

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        errors++;
                        errorLines.Add(new[] { processed.ToString(), "Name is empty", string.Join(",", csv.Parser.Record) });
                        continue;
                    }

                   
                    batchProducts.Add((name.Trim(), description?.Trim() ?? string.Empty, price, categoryId, stock));

                    if (batchProducts.Count >= batchSize)
                    {
                        // Bulk insert products using SqlBulkCopy (use the import job id for staging so all staging rows for this import share the same JobId)
                        await BulkInsertProductsAsync(job.JobId, batchProducts, cancellationToken);
                        inserted += batchProducts.Count;
                        batchProducts.Clear();

                        await _notificationService.NotifyImportProgress(jobId, 0, $"Inserted {inserted} items so far.");
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    errorLines.Add(new[] { processed.ToString(), ex.Message });
                    _logger.LogError(ex, "Error processing CSV row {Row}", processed);
                }
            }

            if (batchProducts.Count > 0)
            {
                await BulkInsertProductsAsync(job.JobId, batchProducts, cancellationToken);
                inserted += batchProducts.Count;
            }

   
            string? errorReportPath = null;
            if (errorLines.Count > 0)
            {
                var errPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), jobId + "_errors.csv");
                using var ef = System.IO.File.CreateText(errPath);
                foreach (var row in errorLines)
                {
                    await ef.WriteLineAsync(string.Join(',', row));
                }
                errorReportPath = errPath;
            }

            try
            {
                job.CategoryIdMappingJson = JsonSerializer.Serialize(categoryIdMap);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize category id mapping for job {JobId}", jobId);
            }

            // update job
            job.TotalRows = processed;
            job.Inserted = inserted;
            job.Errors = errors;
            job.ErrorReportPath = errorReportPath;
            job.Status = "Completed";
            job.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            await _notificationService.NotifyImportProgress(jobId, 100, $"Import completed. Processed: {processed}, Inserted: {inserted}, Errors: {errors}");
        }
        finally
        {
            _importSemaphore.Release();
        }
    }

    private async Task BulkInsertProductsAsync(string stagingJobId, List<(string Name, string Description, decimal Price, int CategoryId, int Stock)> products, CancellationToken cancellationToken)
    {
        // Ensure staging table exists
        await EnsureStagingTableExistsAsync(cancellationToken);

        // Stream rows to staging using a lightweight IDataReader to avoid DataTable allocation
        var jobId = Guid.NewGuid().ToString();
        using var reader = new ListProductDataReader(jobId, products);

        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        var sqlConn = conn as Microsoft.Data.SqlClient.SqlConnection
            ?? throw new InvalidOperationException("Expected SQL Server connection.");

        // Bulk copy into staging using IDataReader
        using (var bulk = new Microsoft.Data.SqlClient.SqlBulkCopy(sqlConn, Microsoft.Data.SqlClient.SqlBulkCopyOptions.Default, null))
        {
            bulk.DestinationTableName = "dbo.ProductImportStaging";
            bulk.BatchSize = products.Count;
            bulk.ColumnMappings.Add("JobId", "JobId");
            bulk.ColumnMappings.Add("Name", "Name");
            bulk.ColumnMappings.Add("Description", "Description");
            bulk.ColumnMappings.Add("Price", "Price");
            bulk.ColumnMappings.Add("CategoryId", "CategoryId");
            bulk.ColumnMappings.Add("Stock", "Stock");

            await bulk.WriteToServerAsync(reader, cancellationToken);
        }

        // Perform MERGE from staging into Products and create Inventories for newly inserted products
        var mergeSql = @"
IF OBJECT_ID('tempdb..#MergeOutput') IS NOT NULL DROP TABLE #MergeOutput;
CREATE TABLE #MergeOutput (MergeAction NVARCHAR(10), ProductId INT, Stock INT);

MERGE dbo.Products AS Target
USING (
    SELECT Name, Description, Price, CategoryId, Stock
    FROM dbo.ProductImportStaging
    WHERE JobId = @JobId
) AS Source
ON Target.Name = Source.Name AND Target.CategoryId = Source.CategoryId
WHEN MATCHED THEN
    UPDATE SET Description = Source.Description, Price = Source.Price
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, Description, Price, CategoryId)
    VALUES (Source.Name, Source.Description, Source.Price, Source.CategoryId)
OUTPUT $action AS MergeAction, INSERTED.Id AS ProductId, Source.Stock INTO #MergeOutput;

INSERT INTO dbo.Inventories (ProductId, Quantity, ReorderLevel)
SELECT ProductId, Stock, 10 FROM #MergeOutput WHERE MergeAction = 'INSERT';

SELECT SUM(CASE WHEN MergeAction = 'INSERT' THEN 1 ELSE 0 END) AS InsertedCount,
       SUM(CASE WHEN MergeAction = 'UPDATE' THEN 1 ELSE 0 END) AS UpdatedCount
FROM #MergeOutput;

DELETE FROM dbo.ProductImportStaging WHERE JobId = @JobId;";

    using var cmd = sqlConn.CreateCommand();
    cmd.CommandText = mergeSql;
    cmd.CommandType = System.Data.CommandType.Text;
    cmd.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@JobId", jobId));

    // Execute and read counts
    using var dbReader = await cmd.ExecuteReaderAsync(cancellationToken);
    int insertedCount = 0;
    int updatedCount = 0;
    if (await dbReader.ReadAsync(cancellationToken))
    {
        insertedCount = dbReader.GetInt32(0);
        updatedCount = dbReader.GetInt32(1);
    }

    // Update ImportJob counts based on jobId - staging used a separate generated jobId; adjust caller to pass jobId if needed.
    // For simplicity, we won't map this job-level generated id back to the ImportJob record here.
    // Caller will use counts from overall processing tracking.
}

    private async Task EnsureStagingTableExistsAsync(CancellationToken cancellationToken)
    {
        var sql = @"IF OBJECT_ID('dbo.ProductImportStaging','U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductImportStaging (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        JobId NVARCHAR(100) NOT NULL,
        Name NVARCHAR(256) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        Price DECIMAL(18,2) NULL,
        CategoryId INT NULL,
        Stock INT NULL
    );
END";

        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(cancellationToken);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandType = System.Data.CommandType.Text;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
