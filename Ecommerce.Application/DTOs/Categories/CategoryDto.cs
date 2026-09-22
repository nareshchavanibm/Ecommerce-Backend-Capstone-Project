namespace ECommerce.Application.DTOs.Categories;

public class CategoryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ProductCount { get; set; }
}