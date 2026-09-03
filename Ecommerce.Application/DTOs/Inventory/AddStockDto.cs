using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Inventory;

public class AddStockDto
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }
}