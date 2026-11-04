using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentService.Api.Controllers;
using PaymentService.Application.Interfaces;
using PaymentDb = PaymentService.Infrastructure.Data.AppDbContext;

namespace Services.IntegrationTests;

public sealed class PaymentApiFactory : WebApplicationFactory<PaymentsController>
{
    public StubOrderClient Orders { get; } = new();

    public PaymentApiFactory()
    {
        TestSettings.Apply();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            TestSettings.UseInMemoryDatabase<PaymentDb>(services);

            // The OrderService is a separate process; its orders are scripted here.
            services.RemoveAll<IOrderClient>();
            services.AddSingleton<IOrderClient>(Orders);
        });
    }
}

public sealed class StubOrderClient : IOrderClient
{
    private readonly Dictionary<int, OrderSnapshot> _orders = new();

    private int _nextId = 1;

    public List<int> Confirmed { get; } = new();

    public List<int> CashOnDelivery { get; } = new();

    public OrderSnapshot Add(int userId, decimal total, string status = "Pending")
    {
        var id = _nextId++;
        var order = new OrderSnapshot(id, userId, total, status, $"ORD-TEST{id:0000}");

        _orders[id] = order;

        return order;
    }

    public Task<OrderSnapshot?> GetOrderAsync(int orderId) =>
        Task.FromResult(_orders.GetValueOrDefault(orderId));

    public Task<OrderSnapshot?> GetOrderByNumberAsync(string orderNumber) =>
        Task.FromResult(_orders.Values.FirstOrDefault(x => x.OrderNumber == orderNumber));

    public Task MarkPaidAsync(int orderId)
    {
        Confirmed.Add(orderId);
        _orders[orderId] = _orders[orderId] with { Status = "Confirmed" };

        return Task.CompletedTask;
    }

    public Task ConfirmCashOnDeliveryAsync(int orderId)
    {
        CashOnDelivery.Add(orderId);
        _orders[orderId] = _orders[orderId] with { Status = "Confirmed" };

        return Task.CompletedTask;
    }
}
