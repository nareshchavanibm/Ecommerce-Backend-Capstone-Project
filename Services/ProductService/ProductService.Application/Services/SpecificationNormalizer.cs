using ProductService.Application.DTOs.Products;
using ProductService.Domain.Entities;

namespace ProductService.Application.Services
{
    // Shared by the admin form and the bulk import so both enforce the same rules.
    public static class SpecificationNormalizer
    {
        public const string DefaultGroup = "General";
        public const int MaxPerProduct = 60;
        public const int MaxGroupLength = 100;
        public const int MaxNameLength = 100;
        public const int MaxValueLength = 500;

        // Blank rows are dropped; the first problem found is returned as an error message.
        public static string? TryNormalize(
            IEnumerable<ProductSpecificationDto>? input,
            out List<ProductSpecification> result)
        {
            result = new List<ProductSpecification>();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in input ?? Enumerable.Empty<ProductSpecificationDto>())
            {
                var group = item.Group?.Trim();
                var name = item.Name?.Trim();
                var value = item.Value?.Trim();

                if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(value))
                    continue;

                if (string.IsNullOrEmpty(name))
                    return "Every specification needs a name.";

                if (string.IsNullOrEmpty(value))
                    return $"Specification \"{name}\" needs a value.";

                if (string.IsNullOrEmpty(group))
                    group = DefaultGroup;

                if (group.Length > MaxGroupLength)
                    return $"Specification group cannot exceed {MaxGroupLength} characters.";

                if (name.Length > MaxNameLength)
                    return $"Specification name cannot exceed {MaxNameLength} characters.";

                if (value.Length > MaxValueLength)
                    return $"Specification \"{name}\" cannot exceed {MaxValueLength} characters.";

                if (!seen.Add($"{group}\u0001{name}"))
                    return $"Specification \"{name}\" is listed twice under \"{group}\".";

                if (result.Count >= MaxPerProduct)
                    return $"A product can have at most {MaxPerProduct} specifications.";

                result.Add(new ProductSpecification
                {
                    Group = group,
                    Name = name,
                    Value = value,
                    SortOrder = result.Count
                });
            }

            return null;
        }

        public static List<ProductSpecificationDto> ToDtos(IEnumerable<ProductSpecification> specifications) =>
            specifications
                .OrderBy(x => x.SortOrder)
                .Select(x => new ProductSpecificationDto { Group = x.Group, Name = x.Name, Value = x.Value })
                .ToList();
    }
}
