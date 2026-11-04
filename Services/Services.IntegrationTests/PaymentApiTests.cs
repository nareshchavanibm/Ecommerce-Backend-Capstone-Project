using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace Services.IntegrationTests;

public class PaymentApiTests(PaymentApiFactory factory) : IClassFixture<PaymentApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static int NewUserId() => Random.Shared.Next(10_000, 1_000_000);

    private HttpClient Customer(int userId) => factory.CreateClient().AsCustomer(userId);

    private static ProcessPaymentRequest Pay(string method = "Card") => new() { PaymentMethod = method };

    private static async Task<PaymentResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PaymentResponse>(Json))!;

    private OrderSnapshot NewOrder(int userId, decimal total = 100m, string status = "Pending") =>
        factory.Orders.Add(userId, total, status);

    [Fact]
    public async Task Pay_WithoutToken_ReturnsUnauthorized()
    {
        var order = NewOrder(1);

        var response = await factory.CreateClient().PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Pay_Card_SucceedsAndConfirmsOrder()
    {
        var userId = NewUserId();
        var order = NewOrder(userId, 450.50m);

        var response = await Customer(userId).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay("Card"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payment = await ReadAsync(response);

        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Equal(450.50m, payment.Amount);
        Assert.StartsWith("SIM-", payment.TransactionId);
        Assert.Equal(order.OrderNumber, payment.OrderNumber);
        Assert.Contains(order.Id, factory.Orders.Confirmed);
    }

    [Fact]
    public async Task Pay_Upi_Succeeds()
    {
        var userId = NewUserId();
        var order = NewOrder(userId);

        var payment = await ReadAsync(
            await Customer(userId).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay("UPI")));

        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Equal(PaymentMethod.UPI, payment.PaymentMethod);
    }

    [Fact]
    public async Task Pay_CashOnDelivery_StaysPendingAndConfirmsOrder()
    {
        var userId = NewUserId();
        var order = NewOrder(userId);

        var payment = await ReadAsync(
            await Customer(userId).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay("CashOnDelivery")));

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.TransactionId);
        Assert.Contains(order.Id, factory.Orders.CashOnDelivery);
    }

    [Fact]
    public async Task Pay_UsesTheOrderTotalNotAClientAmount()
    {
        var userId = NewUserId();
        var order = NewOrder(userId, 75m);

        var payment = await ReadAsync(
            await Customer(userId).PostAsJsonAsync(
                $"/api/payments/orders/{order.OrderNumber}",
                new { paymentMethod = "Card", amount = 1 }));

        Assert.Equal(75m, payment.Amount);
    }

    [Fact]
    public async Task Pay_DeclinedTestAmount_ReturnsFailedWithReason()
    {
        var userId = NewUserId();
        var order = NewOrder(userId, 20.13m);

        var response = await Customer(userId).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payment = await ReadAsync(response);

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Contains("declined", payment.FailureReason);
        Assert.DoesNotContain(order.Id, factory.Orders.Confirmed);
    }

    [Fact]
    public async Task Pay_InsufficientFundsTestAmount_ReturnsFailed()
    {
        var userId = NewUserId();
        var order = NewOrder(userId, 20.14m);

        var payment = await ReadAsync(
            await Customer(userId).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay()));

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("Insufficient funds.", payment.FailureReason);
    }

    [Fact]
    public async Task Pay_ProviderOutageTestAmount_ReturnsBadGateway()
    {
        var userId = NewUserId();
        var order = NewOrder(userId, 20.15m);

        var response = await Customer(userId).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.DoesNotContain(order.Id, factory.Orders.Confirmed);
    }

    [Fact]
    public async Task Pay_AfterADecline_IsNotBlockedAsADuplicate()
    {
        var userId = NewUserId();
        var client = Customer(userId);
        var declined = NewOrder(userId, 30.13m);

        var first = await ReadAsync(await client.PostAsJsonAsync($"/api/payments/orders/{declined.OrderNumber}", Pay()));

        Assert.Equal(PaymentStatus.Failed, first.Status);

        // A failed attempt does not block another attempt on the same order.
        var second = await client.PostAsJsonAsync($"/api/payments/orders/{declined.OrderNumber}", Pay());

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Pay_Twice_SecondAttemptIsRejected()
    {
        var userId = NewUserId();
        var client = Customer(userId);
        var order = NewOrder(userId);

        await client.PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay());

        var response = await client.PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pay_AnotherCustomersOrder_ReturnsForbidden()
    {
        var order = NewOrder(NewUserId());

        var response = await Customer(NewUserId()).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Pay_UnknownOrder_ReturnsNotFound()
    {
        var response = await Customer(NewUserId()).PostAsJsonAsync("/api/payments/orders/ORD-NOPE0000", Pay());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Pay_OrderNotPending_ReturnsBadRequest()
    {
        var userId = NewUserId();
        var order = NewOrder(userId, status: "Cancelled");

        var response = await Customer(userId).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Bitcoin")]
    [InlineData("")]
    public async Task Pay_InvalidMethod_ReturnsBadRequest(string method)
    {
        var userId = NewUserId();
        var order = NewOrder(userId);

        var response = await Customer(userId).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay(method));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OrderPayments_ShowOnlyTheCallersPayments()
    {
        var userId = NewUserId();
        var client = Customer(userId);
        var order = NewOrder(userId);

        await client.PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay());

        var mine = await client.GetFromJsonAsync<List<PaymentResponse>>($"/api/payments/order/{order.OrderNumber}", Json);
        var others = await Customer(NewUserId()).GetFromJsonAsync<List<PaymentResponse>>($"/api/payments/order/{order.OrderNumber}", Json);

        Assert.Single(mine!);
        Assert.Empty(others!);
    }

    [Fact]
    public async Task GetPayment_AsAnotherCustomer_ReturnsForbidden()
    {
        var userId = NewUserId();
        var order = NewOrder(userId);

        var payment = await ReadAsync(
            await Customer(userId).PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay()));

        var response = await Customer(NewUserId()).GetAsync($"/api/payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_AsOwner_ReturnsPayment()
    {
        var userId = NewUserId();
        var client = Customer(userId);
        var order = NewOrder(userId);

        var payment = await ReadAsync(await client.PostAsJsonAsync($"/api/payments/orders/{order.OrderNumber}", Pay()));

        var response = await client.GetAsync($"/api/payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AsCustomer_ReturnsForbidden()
    {
        var response = await Customer(NewUserId()).PutAsync("/api/payments/1/status?status=Successful", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
