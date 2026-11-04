namespace ProductService.Application.DTOs.Products
{
    public class ProductResponseDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        // Filled from live inventory on every read; null when the product has no stock record.
        public int? StockQuantity { get; set; }

        // Only filled when a single product is fetched, not in listings.
        public List<ProductSpecificationDto> Specifications { get; set; } = new();
    }
}