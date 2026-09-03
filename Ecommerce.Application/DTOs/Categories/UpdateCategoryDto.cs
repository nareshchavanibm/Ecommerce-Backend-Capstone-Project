using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Categories;

public class UpdateCategoryDto
{
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(
        100,
        MinimumLength = 2,
        ErrorMessage = "Category name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;


    [Required(ErrorMessage = "Category description is required.")]
    [StringLength(
        500,
        MinimumLength = 5,
        ErrorMessage = "Category description must be between 5 and 500 characters.")]
    public string Description { get; set; } = string.Empty;
}