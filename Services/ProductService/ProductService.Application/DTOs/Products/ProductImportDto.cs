namespace ProductService.Application.DTOs.Products
{
    public class ProductImportRow
    {
        public int RowNumber { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Price { get; set; }

        public string? CategoryName { get; set; }

        public string? CategoryId { get; set; }

        public string? ImageUrl { get; set; }

        public string? StockQuantity { get; set; }

        public string? ReorderLevel { get; set; }
    }

    public class ProductImportResult
    {
        public int TotalRows { get; set; }

        public int Inserted { get; set; }

        public int Errors { get; set; }

        public List<string> ErrorDetails { get; set; } = new();
    }
}
