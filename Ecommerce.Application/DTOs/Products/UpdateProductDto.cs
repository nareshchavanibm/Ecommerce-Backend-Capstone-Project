using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Products;

public class UpdateProductDto
{
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(
        100,
        MinimumLength = 2,
        ErrorMessage = "Product name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;


    [Required(ErrorMessage = "Product description is required.")]
    [StringLength(
        1000,
        MinimumLength = 5,
        ErrorMessage = "Product description must be between 5 and 1000 characters.")]
    public string Description { get; set; } = string.Empty;


    [Range(
        0.01,
        999999999,
        ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }


    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "CategoryId must be greater than 0.")]
    public int CategoryId { get; set; }
}