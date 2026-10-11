using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

public sealed record GatewayResult(bool Succeeded, string? TransactionId, string? FailureReason);

// Seam for a real payment provider. Never pass card details through this service.
public interface IPaymentGateway
{
    Task<GatewayResult> ChargeAsync(decimal amount, PaymentMethod method);
}
