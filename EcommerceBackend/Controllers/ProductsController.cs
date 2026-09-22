using ECommerce.Application.DTOs.Products;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using ECommerce.Infrastructure.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController(IProductService productService, IBackgroundTaskQueue taskQueue, IServiceScopeFactory scopeFactory) : ControllerBase
{
    private readonly IProductService _productService = productService;
    private readonly IBackgroundTaskQueue _taskQueue = taskQueue;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ProductDto>>> GetAll()
    {
        var products = await _productService.GetAllAsync();

        return Ok(products);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDto>> GetById(int id)
    {
        var product = await _productService.GetByIdAsync(id);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create(
        CreateProductDto dto)
    {
        var product = await _productService.CreateAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            product);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductPagedResultDto>> SearchProducts(
        [FromQuery] ProductQueryDto query)
    {
        var paged = await _productService.GetFilteredAsync(query);

        return Ok(paged);
    }

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ImportProducts(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
            return Unauthorized();

        if (!int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized();

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ext != ".csv" && ext != ".xlsx")
            return BadRequest(
                "Unsupported file type. Use .csv or .xlsx.");

        var jobId = Guid.NewGuid().ToString();

        var tempPath =
            Path.Combine(Path.GetTempPath(), jobId + ext);

        try
        {
            await using (var fs = System.IO.File.Create(tempPath))
            {
                await file.CopyToAsync(fs);
            }

            using var scope = _scopeFactory.CreateScope();

            var importer =
                scope.ServiceProvider
                    .GetRequiredService<
                        ECommerce.Infrastructure.Services.ImportProductsService>();

            if (ext == ".csv")
            {
                await importer.ProcessCsvAsync(
                    tempPath,
                    userId,
                    jobId,
                    HttpContext.RequestAborted);
            }
            else
            {
                System.Text.Encoding.RegisterProvider(
                    System.Text.CodePagesEncodingProvider.Instance);

                using var fsRead =
                    System.IO.File.OpenRead(tempPath);

                using var reader =
                    ExcelDataReader.ExcelReaderFactory
                        .CreateReader(fsRead);

                var csvPath =
                    Path.ChangeExtension(tempPath, ".csv");

                using (var sw = System.IO.File.CreateText(csvPath))
                {
                    while (reader.Read())
                    {
                        var cols =
                            new string[reader.FieldCount];

                        for (int i = 0;
                             i < reader.FieldCount;
                             i++)
                        {
                            var value =
                                reader.GetValue(i);

                            var text =
                                value?.ToString() ?? string.Empty;

                            if (text.Contains('"'))
                                text = text.Replace(
                                    "\"",
                                    "\"\"");

                            if (
                                text.Contains(',') ||
                                text.Contains('"') ||
                                text.Contains('\n') ||
                                text.Contains('\r'))
                            {
                                text = $"\"{text}\"";
                            }

                            cols[i] = text;
                        }

                        await sw.WriteLineAsync(
                            string.Join(',', cols));
                    }
                }

                await importer.ProcessCsvAsync(
                    csvPath,
                    userId,
                    jobId,
                    HttpContext.RequestAborted);

                try
                {
                    System.IO.File.Delete(csvPath);
                }
                catch
                {
                    // Ignore cleanup failure
                }
            }

            return Ok(new
            {
                JobId = jobId,
                Message = "Products imported successfully."
            });
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
            catch
            {
                // Ignore cleanup failure
            }
        }
    }

    [HttpPost("import/bulk-upload")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ImportProductsSync(IFormFile file)
    {
        // Extra server-side guard in case role-based attribute is not applied
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized();

        if (!int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized();

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".csv" && ext != ".xlsx")
            return BadRequest("Unsupported file type. Use .csv or .xlsx (xlsx support requires additional library). ");

        var jobId = Guid.NewGuid().ToString();

        var tempPath = Path.Combine(Path.GetTempPath(), jobId + ext);
        await using (var fs = System.IO.File.Create(tempPath))
        {
            await file.CopyToAsync(fs);
        }

        // Queue the bulk upload to background worker to ensure batching and avoid request timeouts
        await _taskQueue.QueueBackgroundWorkItemAsync(async ct =>
        {
            using var scope = _scopeFactory.CreateScope();
            var importer = scope.ServiceProvider.GetRequiredService<ECommerce.Infrastructure.Services.ImportProductsService>();
            var notification = scope.ServiceProvider.GetRequiredService<ECommerce.Application.Interfaces.INotificationService>();

            if (ext == ".csv")
            {
                await importer.ProcessCsvAsync(tempPath, userId, jobId, ct);
            }
            else
            {
                try
                {
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                    using var fsRead = System.IO.File.OpenRead(tempPath);
                    using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(fsRead);

                    var csvPath = Path.ChangeExtension(tempPath, ".csv");
                    using (var sw = System.IO.File.CreateText(csvPath))
                    {
                        while (reader.Read())
                        {
                            var cols = new string[reader.FieldCount];
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var val = reader.GetValue(i);
                                var s = val?.ToString() ?? string.Empty;
                                if (s.Contains('"')) s = s.Replace("\"", "\"\"");
                                if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                                    s = $"\"{s}\"";
                                cols[i] = s;
                            }
                            await sw.WriteLineAsync(string.Join(',', cols));
                        }
                    }

                    await importer.ProcessCsvAsync(csvPath, userId, jobId, ct);

                    try { System.IO.File.Delete(csvPath); } catch { }
                }
                catch (Exception ex)
                {
                    await notification.NotifyImportProgress(jobId, 0, "XLSX import failed: " + ex.Message);
                }
            }

            try { System.IO.File.Delete(tempPath); } catch { }
        });

        return Accepted(new { JobId = jobId, Message = "Import queued and will be processed in background." });
    }

    [HttpGet("import/{jobId}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetImportStatus(string jobId)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ECommerce.Infrastructure.Data.AppDbContext>();

        var job = await db.ImportJobs
            .FirstOrDefaultAsync(j => j.JobId == jobId);

        if (job == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            job.JobId,
            job.Status,
            job.CreatedAt,
            job.CompletedAt,
            job.TotalRows,
            job.Inserted,
            job.Errors,
            HasErrorReport =
                !string.IsNullOrEmpty(job.ErrorReportPath)
        });
    }

    [HttpDelete("{id:int}")]
  
    [Authorize(Roles = "Admin")]

    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _productService.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}