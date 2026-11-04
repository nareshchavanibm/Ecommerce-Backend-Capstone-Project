using ProductService.Domain.Common;

namespace ProductService.Domain.Entities
{
    // One row of a product's specification sheet, e.g. Group "Display", Name "Screen Size", Value "6.5 inch".
    public class ProductSpecification : BaseEntity
    {
        public int ProductId { get; set; }

        public string Group { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public Product? Product { get; set; }
    }
}
