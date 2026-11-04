using PaymentService.Application.Services;
using PaymentService.Domain.Entities;

namespace Services.Tests;

public class SimulatedPaymentGatewayTests
{
    private static SimulatedPaymentGateway Gateway(double failureRate = 0, int delayMs = 0) =>
        new(new PaymentSimulationOptions { FailureRate = failureRate, DelayMs = delayMs });

    [Theory]
    [InlineData("450.50")]
    [InlineData("10")]
    [InlineData("99.99")]
    public async Task Ordinary_amounts_are_approved_with_a_transaction_id(string amount)
    {
        var result = await Gateway().ChargeAsync(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), PaymentMethod.Card);

        Assert.True(result.Succeeded);
        Assert.StartsWith("SIM-", result.TransactionId);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task Each_approval_gets_a_new_transaction_id()
    {
        var gateway = Gateway();

        var first = await gateway.ChargeAsync(10m, PaymentMethod.Card);
        var second = await gateway.ChargeAsync(10m, PaymentMethod.UPI);

        Assert.NotEqual(first.TransactionId, second.TransactionId);
    }

    [Theory]
    [InlineData("10.13", "declined by the bank")]
    [InlineData("250.14", "Insufficient funds")]
    public async Task Test_amounts_are_declined_with_a_reason(string amount, string reason)
    {
        var result = await Gateway().ChargeAsync(
            decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            PaymentMethod.Card);

        Assert.False(result.Succeeded);
        Assert.Null(result.TransactionId);
        Assert.Contains(reason, result.FailureReason);
    }

    [Fact]
    public async Task The_provider_outage_test_amount_throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Gateway().ChargeAsync(10.15m, PaymentMethod.Card));
    }

    [Fact]
    public async Task A_failure_rate_of_one_declines_every_charge()
    {
        var result = await Gateway(failureRate: 1).ChargeAsync(10m, PaymentMethod.Card);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task A_failure_rate_of_zero_never_declines()
    {
        var gateway = Gateway(failureRate: 0);

        for (var i = 0; i < 50; i++)
        {
            Assert.True((await gateway.ChargeAsync(10m, PaymentMethod.Card)).Succeeded);
        }
    }

    [Fact]
    public async Task The_configured_delay_is_applied()
    {
        var started = DateTime.UtcNow;

        await Gateway(delayMs: 120).ChargeAsync(10m, PaymentMethod.Card);

        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(100));
    }
}
