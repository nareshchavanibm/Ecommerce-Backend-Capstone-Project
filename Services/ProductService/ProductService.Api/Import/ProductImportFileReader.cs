using System.Globalization;
using ExcelDataReader;
using ProductService.Application.DTOs.Products;

namespace ProductService.Api.Import
{
    public static class ProductImportFileReader
    {
        public const int MaxRows = 10000;

        // Reads a CSV or XLSX file; the first row must be the header.
        public static List<ProductImportRow> Read(Stream stream, string extension)
        {
            using var reader = extension == ".csv"
                ? ExcelReaderFactory.CreateCsvReader(stream)
                : ExcelReaderFactory.CreateReader(stream);

            var rows = new List<ProductImportRow>();
            Dictionary<string, int>? columns = null;
            var rowNumber = 0;

            while (reader.Read())
            {
                rowNumber++;

                if (columns == null)
                {
                    columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var header = Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)?.Trim();

                        if (!string.IsNullOrEmpty(header))
                            columns.TryAdd(header, i);
                    }

                    if (!columns.ContainsKey("Name") || !columns.ContainsKey("Price"))
                        throw new InvalidDataException("The file must have Name and Price columns in the first row.");

                    continue;
                }

                string? Cell(string column)
                {
                    if (!columns.TryGetValue(column, out var index) || index >= reader.FieldCount)
                        return null;

                    return Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture);
                }

                var row = new ProductImportRow
                {
                    RowNumber = rowNumber,
                    Name = Cell("Name"),
                    Description = Cell("Description"),
                    Price = Cell("Price"),
                    CategoryName = Cell("CategoryName"),
                    CategoryId = Cell("CategoryId"),
                    ImageUrl = Cell("ImageUrl"),
                    StockQuantity = Cell("StockQuantity") ?? Cell("Stock") ?? Cell("InitialStock"),
                    ReorderLevel = Cell("ReorderLevel")
                };

                if (string.IsNullOrWhiteSpace(row.Name) && string.IsNullOrWhiteSpace(row.Price))
                    continue;

                if (rows.Count >= MaxRows)
                    throw new InvalidDataException($"A maximum of {MaxRows} rows can be imported at once.");

                rows.Add(row);
            }

            if (columns == null)
                throw new InvalidDataException("The file is empty.");

            return rows;
        }
    }
}
