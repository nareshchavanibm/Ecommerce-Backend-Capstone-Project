using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Cart;

public class AddCartItemDto
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "ProductId must be greater than 0.")]
    public int ProductId { get; set; }


    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }
}