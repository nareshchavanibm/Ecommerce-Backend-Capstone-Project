using System.ComponentModel.DataAnnotations;

namespace ECommerce.Application.DTOs.Payments;

public class CreatePaymentDto
{
    [Required(ErrorMessage = "Payment method is required.")]
    [StringLength(
        50,
        MinimumLength = 2,
        ErrorMessage = "Payment method must be between 2 and 50 characters.")]
    public string PaymentMethod { get; set; } = string.Empty;
}