namespace ECommerce.Application.DTOs.Payments;

public class PaymentDto
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? TransactionId { get; set; }

    public DateTime PaymentDate { get; set; }
}