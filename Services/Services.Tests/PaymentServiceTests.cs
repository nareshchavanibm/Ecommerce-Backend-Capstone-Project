using Microsoft.Extensions.Logging.Abstractions;
using PaymentService.Application.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaySvc = PaymentService.Application.Services.PaymentService;

namespace Services.Tests;

internal sealed class FakePaymentRepository : IPaymentRepository
{
    private int _nextId = 1;

    public List<Payment> Payments { get; } = new();

    public Task<Payment> CreateAsync(Payment payment)
    {
        payment.Id = _nextId++;
        Payments.Add(payment);

        return Task.FromResult(payment);
    }

    public Task<Payment?> GetByIdAsync(int id) =>
        Task.FromResult(Payments.FirstOrDefault(x => x.Id == id));

    public Task<IEnumerable<Payment>> GetByOrderIdAsync(int orderId) =>
        Task.FromResult<IEnumerable<Payment>>(Payments.Where(x => x.OrderId == orderId).ToList());

    public Task<Payment?> UpdateStatusAsync(int id, PaymentStatus status)
    {
        var payment = Payments.FirstOrDefault(x => x.Id == id);

        if (payment != null)
        {
            payment.Status = status;
        }

        return Task.FromResult(payment);
    }

    public Task UpdateAsync(Payment payment) => Task.CompletedTask;
}

internal sealed class FakeOrderClient : IOrderClient
{
    public OrderSnapshot? Order { get; set; } = new(10, 7, 450.50m, "Pending");

    public List<int> Paid { get; } = new();

    public Exception? MarkPaidFails { get; set; }

    public Task<OrderSnapshot?> GetOrderAsync(int orderId) =>
        Task.FromResult(Order is not null && Order.Id == orderId ? Order : null);

    public Task<OrderSnapshot?> GetOrderByNumberAsync(string orderNumber) =>
        Task.FromResult(Order is not null && Order.OrderNumber == orderNumber ? Order : null);

    public Task MarkPaidAsync(int orderId)
    {
        if (MarkPaidFails != null)
        {
            throw MarkPaidFails;
        }

        Paid.Add(orderId);

        return Task.CompletedTask;
    }
}

internal sealed class FakeGateway : IPaymentGateway
{
    public bool Approve { get; set; } = true;

    public decimal? ChargedAmount { get; private set; }

    public Task<GatewayResult> ChargeAsync(decimal amount, PaymentMethod method)
    {
        ChargedAmount = amount;

        return Task.FromResult(Approve
            ? new GatewayResult(true, "tx-1", null)
            : new GatewayResult(false, null, "declined"));
    }
}

public class PaymentServiceTests
{
    private readonly FakePaymentRepository _payments = new();
    private readonly FakeOrderClient _orders = new();
    private readonly FakeGateway _gateway = new();
    private readonly PaySvc _service;

    public PaymentServiceTests()
    {
        _service = new PaySvc(_payments, _orders, _gateway, NullLogger<PaySvc>.Instance);
    }

    [Fact]
    public async Task Successful_payment_charges_the_order_total_and_confirms_the_order()
    {
        var payment = await _service.ProcessPaymentAsync(7, 10, "Card");

        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Equal(450.50m, payment.Amount);
        Assert.Equal(450.50m, _gateway.ChargedAmount);
        Assert.Equal("tx-1", payment.TransactionId);
        Assert.Equal(new[] { 10 }, _orders.Paid);
    }

    [Theory]
    [InlineData("card")]
    [InlineData("UPI")]
    public async Task Card_and_upi_are_accepted_case_insensitively(string method)
    {
        var payment = await _service.ProcessPaymentAsync(7, 10, method);

        Assert.Equal(PaymentStatus.Successful, payment.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bitcoin")]
    [InlineData("99")]
    public async Task Unknown_payment_methods_are_rejected(string method)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ProcessPaymentAsync(7, 10, method));

        Assert.Empty(_payments.Payments);
    }

    [Fact]
    public async Task Cash_on_delivery_is_not_accepted_as_an_online_payment()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ProcessPaymentAsync(7, 10, "CashOnDelivery"));

        Assert.Empty(_orders.Paid);
    }

    [Fact]
    public async Task Users_cannot_pay_for_someone_elses_order()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.ProcessPaymentAsync(8, 10, "Card"));

        Assert.Empty(_payments.Payments);
    }

    [Fact]
    public async Task Unknown_orders_are_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ProcessPaymentAsync(7, 999, "Card"));
    }

    [Theory]
    [InlineData("Confirmed")]
    [InlineData("Cancelled")]
    public async Task Only_pending_orders_can_be_paid(string status)
    {
        _orders.Order = new OrderSnapshot(10, 7, 100m, status);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ProcessPaymentAsync(7, 10, "Card"));
    }

    [Fact]
    public async Task A_second_payment_for_the_same_order_is_rejected()
    {
        await _service.ProcessPaymentAsync(7, 10, "Card");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.ProcessPaymentAsync(7, 10, "Card"));

        Assert.Contains("already exists", error.Message);
        Assert.Single(_payments.Payments);
    }

    [Fact]
    public async Task A_declined_charge_is_reported_as_failed_and_does_not_confirm_the_order()
    {
        _gateway.Approve = false;

        var payment = await _service.ProcessPaymentAsync(7, 10, "Card");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Empty(_orders.Paid);
    }

    [Fact]
    public async Task A_failed_attempt_can_be_retried()
    {
        _gateway.Approve = false;
        await _service.ProcessPaymentAsync(7, 10, "Card");

        _gateway.Approve = true;
        var retry = await _service.ProcessPaymentAsync(7, 10, "Card");

        Assert.Equal(PaymentStatus.Successful, retry.Status);
        Assert.Equal(2, _payments.Payments.Count);
    }

    [Fact]
    public async Task Payment_is_marked_failed_when_the_order_cannot_be_confirmed()
    {
        _orders.MarkPaidFails = new DependencyUnavailableException("order service down");

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => _service.ProcessPaymentAsync(7, 10, "Card"));

        Assert.Equal(PaymentStatus.Failed, Assert.Single(_payments.Payments).Status);

        // The customer can try again once the order service recovers.
        _orders.MarkPaidFails = null;

        var retry = await _service.ProcessPaymentAsync(7, 10, "Card");

        Assert.Equal(PaymentStatus.Successful, retry.Status);
    }

    [Fact]
    public async Task Payments_for_an_order_can_be_listed()
    {
        await _service.ProcessPaymentAsync(7, 10, "Card");

        var listed = (await _service.GetPaymentsByOrderIdAsync(10)).ToList();

        Assert.Single(listed);
        Assert.Equal(listed[0].PaidAt, listed[0].PaymentDate);
    }
}
