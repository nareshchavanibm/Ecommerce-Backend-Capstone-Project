using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Services;

// Development stand-in: approves every charge. Replace with a real provider integration.
public sealed class MockPaymentGateway : IPaymentGateway
{
    public Task<GatewayResult> ChargeAsync(decimal amount, PaymentMethod method)
    {
        return Task.FromResult(new GatewayResult(true, Guid.NewGuid().ToString(), null));
    }
}
