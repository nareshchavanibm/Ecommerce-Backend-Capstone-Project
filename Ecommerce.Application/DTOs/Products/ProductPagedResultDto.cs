namespace ECommerce.Application.DTOs.Products;

public class ProductPagedResultDto
{
    public List<ProductDto> Products { get; set; } = new();

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
