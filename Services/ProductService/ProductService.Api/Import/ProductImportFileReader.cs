using System.Globalization;
using ExcelDataReader;
using ProductService.Application.DTOs.Products;

namespace ProductService.Api.Import
{
    public static class ProductImportFileReader
    {
        public const int MaxRows = 10000;

        // ExcelDataReader looks up Windows-1252 and .NET does not ship it unless registered.
        static ProductImportFileReader()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        // Reads a CSV or XLSX file; the first row must be the header.
        public static List<ProductImportRow> Read(Stream stream, string extension)
        {
            using var reader = extension == ".csv"
                ? ExcelReaderFactory.CreateCsvReader(stream)
                : ExcelReaderFactory.CreateReader(stream);

            var rows = new List<ProductImportRow>();
            Dictionary<string, int>? columns = null;
            var specColumns = new List<(int Index, string Group, string Name)>();
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

                        if (string.IsNullOrEmpty(header))
                            continue;

                        if (TryParseSpecHeader(header, out var group, out var specName))
                            specColumns.Add((i, group, specName));
                        else
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

                foreach (var (index, group, name) in specColumns)
                {
                    if (index >= reader.FieldCount)
                        continue;

                    var value = Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture);

                    if (!string.IsNullOrWhiteSpace(value))
                        row.Specifications.Add(new ProductSpecificationDto { Group = group, Name = name, Value = value });
                }

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

        // "Spec:Name" goes under General; "Spec:Group:Name" under its own group.
        private static bool TryParseSpecHeader(string header, out string group, out string name)
        {
            group = string.Empty;
            name = string.Empty;

            var parts = header.Split(':', 3, StringSplitOptions.TrimEntries);

            if (parts.Length < 2 ||
                !(parts[0].Equals("Spec", StringComparison.OrdinalIgnoreCase) ||
                  parts[0].Equals("Specification", StringComparison.OrdinalIgnoreCase)))
                return false;

            if (parts.Length == 3)
            {
                group = parts[1];
                name = parts[2];
            }
            else
            {
                name = parts[1];
            }

            return name.Length > 0;
        }
    }
}
