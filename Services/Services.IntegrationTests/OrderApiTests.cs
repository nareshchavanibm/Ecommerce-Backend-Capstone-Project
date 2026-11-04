using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderService.Application.DTOs.Orders;

namespace Services.IntegrationTests;

public class OrderApiTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;

    public OrderApiTests(OrderApiFactory factory)
    {
        _factory = factory;

        _factory.Catalog.Add(1, "Runner", 50m);
        _factory.Catalog.Add(2, "Boot", 120.5m);
        _factory.Catalog.Add(3, "Retired", 10m, isActive: false);

        _factory.Stock.RejectWith = null;
    }

    private HttpClient Anonymous() => _factory.CreateClient();

    private HttpClient Customer(int userId) => _factory.CreateClient().AsCustomer(userId);

    private HttpClient Admin() => _factory.CreateClient().AsAdmin();

    // Each test uses its own customer id so orders from other tests never interfere.
    private static int NewUserId() => Random.Shared.Next(10_000, 1_000_000);

    private static OrderCreateDto Order(params (int ProductId, int Quantity)[] lines) => new()
    {
        Items = lines.Select(x => new OrderItemCreateDto { ProductId = x.ProductId, Quantity = x.Quantity }).ToList()
    };

    private static async Task<OrderResponseDto> PlaceAsync(HttpClient client, OrderCreateDto order)
    {
        var response = await client.PostAsJsonAsync("/api/order", order);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<OrderResponseDto>())!;
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString()!;

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/order", Order((1, 1)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_PricesFromCatalogAndStartsPending()
    {
        var userId = NewUserId();

        var order = await PlaceAsync(Customer(userId), Order((1, 2), (2, 1)));

        Assert.StartsWith("ORD-", order.OrderNumber);
        Assert.Equal(userId, order.UserId);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(2 * 50m + 120.5m, order.TotalAmount);
        Assert.Equal(2, order.Items.Count);
        Assert.Contains(order.Items, x => x.ProductName == "Runner" && x.UnitPrice == 50m && x.Quantity == 2);
    }

    [Fact]
    public async Task Create_IgnoresClientSuppliedUserId()
    {
        var userId = NewUserId();
        var body = Order((1, 1));

        body.UserId = 1;

        var order = await PlaceAsync(Customer(userId), body);

        Assert.Equal(userId, order.UserId);
    }

    [Fact]
    public async Task Create_ReservesStock()
    {
        var before = _factory.Stock.Reserved.Count;

        await PlaceAsync(Customer(NewUserId()), Order((1, 3)));

        var line = Assert.Single(_factory.Stock.Reserved.Skip(before).Last());

        Assert.Equal(1, line.ProductId);
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public async Task Create_RepeatedProduct_IsMergedIntoOneLine()
    {
        var order = await PlaceAsync(Customer(NewUserId()), Order((1, 1), (1, 2)));

        var item = Assert.Single(order.Items);

        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public async Task Create_EmptyItems_ReturnsBadRequest()
    {
        var response = await Customer(NewUserId()).PostAsJsonAsync("/api/order", Order());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -2)]
    public async Task Create_InvalidLine_ReturnsBadRequest(int productId, int quantity)
    {
        var response = await Customer(NewUserId()).PostAsJsonAsync("/api/order", Order((productId, quantity)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownProduct_ReturnsBadRequest()
    {
        var response = await Customer(NewUserId()).PostAsJsonAsync("/api/order", Order((999, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("999", await MessageAsync(response));
    }

    [Fact]
    public async Task Create_InactiveProduct_ReturnsBadRequest()
    {
        var response = await Customer(NewUserId()).PostAsJsonAsync("/api/order", Order((3, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InsufficientStock_ReturnsBadRequestAndStoresNoOrder()
    {
        var userId = NewUserId();
        var client = Customer(userId);

        _factory.Stock.RejectWith = "Insufficient stock for Runner.";

        var response = await client.PostAsJsonAsync("/api/order", Order((1, 500)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Insufficient stock for Runner.", await MessageAsync(response));

        var mine = await client.GetFromJsonAsync<List<OrderResponseDto>>("/api/order/my-orders");

        Assert.Empty(mine!);
    }

    [Fact]
    public async Task MyOrders_ReturnsOnlyCallersOrders()
    {
        var alice = NewUserId();
        var bob = NewUserId();

        await PlaceAsync(Customer(alice), Order((1, 1)));
        await PlaceAsync(Customer(alice), Order((2, 1)));
        await PlaceAsync(Customer(bob), Order((1, 1)));

        var orders = await Customer(alice).GetFromJsonAsync<List<OrderResponseDto>>("/api/order/my-orders");

        Assert.Equal(2, orders!.Count);
        Assert.All(orders, x => Assert.Equal(alice, x.UserId));
    }

    [Fact]
    public async Task GetByNumber_AsOwner_ReturnsOrder()
    {
        var client = Customer(NewUserId());
        var order = await PlaceAsync(client, Order((1, 1)));

        var response = await client.GetAsync($"/api/order/{order.OrderNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetByNumber_IsCaseInsensitive()
    {
        var client = Customer(NewUserId());
        var order = await PlaceAsync(client, Order((1, 1)));

        var response = await client.GetAsync($"/api/order/{order.OrderNumber.ToLowerInvariant()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetByNumber_AsAnotherCustomer_ReturnsForbidden()
    {
        var order = await PlaceAsync(Customer(NewUserId()), Order((1, 1)));

        var response = await Customer(NewUserId()).GetAsync($"/api/order/{order.OrderNumber}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetByNumber_AsAdmin_ReturnsOrder()
    {
        var order = await PlaceAsync(Customer(NewUserId()), Order((1, 1)));

        var response = await Admin().GetAsync($"/api/order/{order.OrderNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetByNumber_Unknown_ReturnsNotFound()
    {
        var response = await Customer(NewUserId()).GetAsync("/api/order/ORD-NOPE0000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsCustomer_ReturnsForbidden()
    {
        var response = await Customer(NewUserId()).GetAsync("/api/order");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsAdmin_ReturnsOrders()
    {
        await PlaceAsync(Customer(NewUserId()), Order((1, 1)));

        var orders = await Admin().GetFromJsonAsync<List<OrderResponseDto>>("/api/order");

        Assert.NotEmpty(orders!);
    }

    [Fact]
    public async Task OrdersByUser_AsOtherCustomer_ReturnsForbidden()
    {
        var owner = NewUserId();

        var response = await Customer(NewUserId()).GetAsync($"/api/order/user/{owner}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_OwnPendingOrder_CancelsAndReleasesStock()
    {
        var client = Customer(NewUserId());
        var order = await PlaceAsync(client, Order((2, 4)));
        var releasedBefore = _factory.Stock.Released.Count;

        var response = await client.PutAsync($"/api/order/{order.OrderNumber}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cancelled = await response.Content.ReadFromJsonAsync<OrderResponseDto>();

        Assert.Equal("Cancelled", cancelled!.Status);

        var released = Assert.Single(_factory.Stock.Released.Skip(releasedBefore).Single());

        Assert.Equal(2, released.ProductId);
        Assert.Equal(4, released.Quantity);
    }

    [Fact]
    public async Task Cancel_AnotherCustomersOrder_ReturnsForbidden()
    {
        var order = await PlaceAsync(Customer(NewUserId()), Order((1, 1)));

        var response = await Customer(NewUserId()).PutAsync($"/api/order/{order.OrderNumber}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_Twice_ReturnsBadRequest()
    {
        var client = Customer(NewUserId());
        var order = await PlaceAsync(client, Order((1, 1)));

        await client.PutAsync($"/api/order/{order.OrderNumber}/cancel", null);

        var response = await client.PutAsync($"/api/order/{order.OrderNumber}/cancel", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_Unknown_ReturnsNotFound()
    {
        var response = await Customer(NewUserId()).PutAsync("/api/order/ORD-NOPE0000/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AsAdmin_MovesOrderForward()
    {
        var order = await PlaceAsync(Customer(NewUserId()), Order((1, 1)));

        var response = await Admin().PutAsJsonAsync(
            $"/api/order/{order.OrderNumber}/status",
            new UpdateOrderStatusDto { Status = "Shipped" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Shipped", (await response.Content.ReadFromJsonAsync<OrderResponseDto>())!.Status);
    }

    [Fact]
    public async Task UpdateStatus_AsCustomer_ReturnsForbidden()
    {
        var client = Customer(NewUserId());
        var order = await PlaceAsync(client, Order((1, 1)));

        var response = await client.PutAsJsonAsync(
            $"/api/order/{order.OrderNumber}/status",
            new UpdateOrderStatusDto { Status = "Shipped" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_ReturnsBadRequest()
    {
        var order = await PlaceAsync(Customer(NewUserId()), Order((1, 1)));

        var response = await Admin().PutAsJsonAsync(
            $"/api/order/{order.OrderNumber}/status",
            new UpdateOrderStatusDto { Status = "Teleported" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_ReopeningCancelledOrder_ReturnsBadRequest()
    {
        var client = Customer(NewUserId());
        var order = await PlaceAsync(client, Order((1, 1)));

        await client.PutAsync($"/api/order/{order.OrderNumber}/cancel", null);

        var response = await Admin().PutAsJsonAsync(
            $"/api/order/{order.OrderNumber}/status",
            new UpdateOrderStatusDto { Status = "Confirmed" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cart_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Anonymous().GetAsync("/api/cart");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cart_NewCustomer_HasEmptyCart()
    {
        var response = await Customer(NewUserId()).GetAsync("/api/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InternalEndpoint_WithoutKey_IsRejected()
    {
        var response = await Anonymous().GetAsync("/internal/orders/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Purchased_OnlyAfterDelivery()
    {
        var userId = NewUserId();
        var order = await PlaceAsync(Customer(userId), Order((1, 1)));

        var internalClient = _factory.CreateClient();
        internalClient.DefaultRequestHeaders.Add("X-Internal-Key", TestSettings.InternalKey);

        async Task<bool> PurchasedAsync(int productId) =>
            (await internalClient.GetFromJsonAsync<JsonElement>(
                $"/internal/orders/purchased?userId={userId}&productId={productId}"))
            .GetProperty("purchased").GetBoolean();

        Assert.False(await PurchasedAsync(1));

        var update = await Admin().PutAsJsonAsync(
            $"/api/order/{order.OrderNumber}/status",
            new UpdateOrderStatusDto { Status = "Delivered" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.True(await PurchasedAsync(1));
        Assert.False(await PurchasedAsync(2));
    }

    [Fact]
    public async Task Purchased_WithoutKey_IsRejected()
    {
        var response = await Anonymous().GetAsync("/internal/orders/purchased?userId=1&productId=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
