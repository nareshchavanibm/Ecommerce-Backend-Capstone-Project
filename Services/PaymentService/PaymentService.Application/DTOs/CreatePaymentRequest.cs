using PaymentService.Domain.Entities;

namespace PaymentService.Application.DTOs;

public class CreatePaymentRequest
{
    public int OrderId { get; set; }

    public int UserId { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
}