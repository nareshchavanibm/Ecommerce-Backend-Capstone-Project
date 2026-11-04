using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Services;

public sealed class PaymentSimulationOptions
{
    // Pause that mimics provider latency.
    public int DelayMs { get; set; }

    // Chance (0 to 1) that an otherwise valid charge is declined.
    public double FailureRate { get; set; }
}

// Sandbox provider. The cents of the amount choose the outcome:
// .13 declined, .14 insufficient funds, .15 provider outage; anything else is approved.
public sealed class SimulatedPaymentGateway(PaymentSimulationOptions options) : IPaymentGateway
{
    public async Task<GatewayResult> ChargeAsync(decimal amount, PaymentMethod method)
    {
        if (options.DelayMs > 0)
        {
            await Task.Delay(options.DelayMs);
        }

        switch ((int)Math.Round(amount % 1 * 100))
        {
            case 13:
                return Declined("The payment was declined by the bank.");

            case 14:
                return Declined("Insufficient funds.");

            case 15:
                throw new InvalidOperationException("Simulated provider outage.");
        }

        if (options.FailureRate > 0 && Random.Shared.NextDouble() < options.FailureRate)
        {
            return Declined("The payment was declined (simulated).");
        }

        return new GatewayResult(true, "SIM-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), null);
    }

    private static GatewayResult Declined(string reason) => new(false, null, reason);
}
