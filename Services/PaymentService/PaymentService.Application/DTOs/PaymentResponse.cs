using System.Text.Json.Serialization;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.DTOs;

public class ProcessPaymentRequest
{
    public string PaymentMethod { get; set; } = string.Empty;
}

public class PaymentResponse
{
    public int Id { get; set; }

    // Internal only: the public reference to the order is OrderNumber.
    [JsonIgnore]
    public int OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public int UserId { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentStatus Status { get; set; }

    public string? TransactionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    // When the payment completed, otherwise when it was started.
    public DateTime PaymentDate { get; set; }
}
