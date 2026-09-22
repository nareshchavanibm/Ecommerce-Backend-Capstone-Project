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


 
}