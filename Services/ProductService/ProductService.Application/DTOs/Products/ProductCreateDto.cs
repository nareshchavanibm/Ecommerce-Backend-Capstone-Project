namespace ProductService.Application.DTOs.Products
{
    public class ProductCreateDto
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        public int CategoryId { get; set; }

        // Null leaves the existing specifications alone on update; an empty list clears them.
        public List<ProductSpecificationDto>? Specifications { get; set; }
    }

    public class ProductSpecificationDto
    {
        // Section heading such as "Display"; blank means "General".
        public string? Group { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }
}