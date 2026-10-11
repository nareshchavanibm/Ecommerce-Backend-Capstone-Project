using OrderService.Application.DTOs.Cart;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Exceptions;
using OrderService.Application.Interfaces.Clients;
using OrderService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using CartSvc = OrderService.Application.Services.CartService;
using OrderSvc = OrderService.Application.Services.OrderService;

namespace Services.Tests;

public class OrderServiceTests
{
    private readonly FakeProductCatalog _catalog = new();
    private readonly FakeStockService _stock = new();
    private readonly FakeOrderRepository _orders = new();
    private readonly OrderSvc _service;

    public OrderServiceTests()
    {
        _catalog.Products[1] = new CatalogProduct(1, "Trail Shoe", 100m, true);
        _catalog.Products[2] = new CatalogProduct(2, "Road Shoe", 250.50m, true);
        _catalog.Products[3] = new CatalogProduct(3, "Retired Shoe", 80m, false);

        _service = new OrderSvc(
            _orders,
            new FakeOrderItemRepository(_orders),
            _catalog,
            _stock,
            NullLogger<OrderSvc>.Instance);
    }

    private static OrderCreateDto Request(params (int productId, int quantity)[] lines) => new()
    {
        UserId = 7,
        Items = lines.Select(l => new OrderItemCreateDto { ProductId = l.productId, Quantity = l.quantity }).ToList()
    };

    [Fact]
    public async Task Order_total_and_unit_prices_come_from_the_catalog()
    {
        var order = await _service.CreateOrderAsync(Request((1, 2), (2, 1)));

        Assert.Equal(450.50m, order.TotalAmount);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(100m, order.Items.Single(i => i.ProductId == 1).UnitPrice);
        Assert.Equal("Trail Shoe", order.Items.Single(i => i.ProductId == 1).ProductName);
        Assert.Equal(7, order.UserId);
    }

    [Fact]
    public async Task Stock_is_reserved_for_the_ordered_quantities()
    {
        await _service.CreateOrderAsync(Request((1, 2), (2, 1)));

        var reserved = Assert.Single(_stock.Reserved);

        Assert.Equal(2, reserved.Single(l => l.ProductId == 1).Quantity);
        Assert.Equal(1, reserved.Single(l => l.ProductId == 2).Quantity);
    }

    [Fact]
    public async Task Repeated_products_are_merged_into_one_line()
    {
        var order = await _service.CreateOrderAsync(Request((1, 1), (1, 2)));

        var item = Assert.Single(order.Items);

        Assert.Equal(3, item.Quantity);
        Assert.Equal(300m, order.TotalAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_quantities_are_rejected(int quantity)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateOrderAsync(Request((1, quantity))));

        Assert.Empty(_stock.Reserved);
    }

    [Fact]
    public async Task Empty_orders_are_rejected()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateOrderAsync(Request()));
    }

    [Fact]
    public async Task Unknown_or_inactive_products_are_rejected_before_stock_is_touched()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateOrderAsync(Request((99, 1))));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateOrderAsync(Request((3, 1))));

        Assert.Empty(_stock.Reserved);
        Assert.Empty(_orders.Orders);
    }

    [Fact]
    public async Task Insufficient_stock_creates_no_order()
    {
        _stock.RejectWith = "Insufficient stock for Trail Shoe. Available: 1.";

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateOrderAsync(Request((1, 5))));

        Assert.Contains("Insufficient stock", error.Message);
        Assert.Empty(_orders.Orders);
    }

    [Fact]
    public async Task Reserved_stock_is_released_when_the_order_cannot_be_saved()
    {
        _orders.FailOnAdd = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateOrderAsync(Request((1, 2))));

        var released = Assert.Single(_stock.Released);

        Assert.Equal(2, released.Single().Quantity);
    }

    [Fact]
    public async Task Owner_can_cancel_a_pending_order_and_stock_is_released()
    {
        var order = await _service.CreateOrderAsync(Request((1, 2)));

        var cancelled = await _service.CancelOrderAsync(order.OrderNumber, userId: 7, isAdmin: false);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(2, Assert.Single(_stock.Released).Single().Quantity);
    }

    [Fact]
    public async Task Other_users_cannot_cancel_an_order()
    {
        var order = await _service.CreateOrderAsync(Request((1, 1)));

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.CancelOrderAsync(order.OrderNumber, userId: 8, isAdmin: false));

        Assert.Empty(_stock.Released);
    }

    [Fact]
    public async Task Only_pending_orders_can_be_cancelled()
    {
        var order = await _service.CreateOrderAsync(Request((1, 1)));

        await _service.MarkPaidAsync(order.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CancelOrderAsync(order.OrderNumber, userId: 7, isAdmin: false));
    }

    [Fact]
    public async Task Marking_paid_confirms_the_order_and_is_idempotent()
    {
        var order = await _service.CreateOrderAsync(Request((1, 1)));

        var first = await _service.MarkPaidAsync(order.Id);
        var second = await _service.MarkPaidAsync(order.Id);

        Assert.Equal("Confirmed", first.Status);
        Assert.Equal("Confirmed", second.Status);
    }

    [Fact]
    public async Task A_cancelled_order_cannot_be_marked_paid()
    {
        var order = await _service.CreateOrderAsync(Request((1, 1)));

        await _service.CancelOrderAsync(order.OrderNumber, 7, false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.MarkPaidAsync(order.Id));
    }

    [Fact]
    public async Task Admin_status_updates_release_stock_on_cancel_and_block_reopening()
    {
        var order = await _service.CreateOrderAsync(Request((1, 2)));

        await _service.UpdateOrderStatusAsync(order.OrderNumber, "Cancelled");

        Assert.Single(_stock.Released);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateOrderStatusAsync(order.OrderNumber, "Shipped"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateOrderStatusAsync(order.OrderNumber, "Nonsense"));
    }

    [Fact]
    public async Task Each_order_gets_a_public_number_that_finds_it()
    {
        var first = await _service.CreateOrderAsync(Request((1, 1)));
        var second = await _service.CreateOrderAsync(Request((1, 1)));

        Assert.Matches("^ORD-[A-HJKMNP-Z2-9]{8}$", first.OrderNumber);
        Assert.NotEqual(first.OrderNumber, second.OrderNumber);
        Assert.Equal(first.Id, (await _service.GetOrderByNumberAsync(first.OrderNumber))!.Id);
        Assert.Null(await _service.GetOrderByNumberAsync("ORD-UNKNOWN1"));
    }

    [Fact]
    public async Task Order_history_falls_back_to_a_placeholder_when_the_catalog_is_down()
    {
        var order = await _service.CreateOrderAsync(Request((1, 1)));

        _catalog.Unavailable = true;

        var loaded = await _service.GetOrderByIdAsync(order.Id);

        Assert.Equal("Product #1", loaded!.Items.Single().ProductName);
    }
}

public class CartServiceTests
{
    private readonly FakeProductCatalog _catalog = new();
    private readonly FakeCartItemRepository _items = new();
    private readonly CartSvc _service;

    public CartServiceTests()
    {
        _catalog.Products[1] = new CatalogProduct(1, "Trail Shoe", 100m, true);
        _catalog.Products[3] = new CatalogProduct(3, "Retired Shoe", 80m, false);

        _service = new CartSvc(new FakeCartRepository(), _items, _catalog, NullLogger<CartSvc>.Instance);
    }

    [Fact]
    public async Task Adding_an_item_prices_it_from_the_catalog()
    {
        var cart = await _service.AddItemAsync(7, new AddCartItemRequest { ProductId = 1, Quantity = 2 });

        var item = Assert.Single(cart.Items);

        Assert.Equal("Trail Shoe", item.ProductName);
        Assert.Equal(100m, item.Price);
        Assert.Equal(200m, item.TotalPrice);
        Assert.Equal(200m, cart.TotalAmount);
    }

    [Fact]
    public async Task Adding_the_same_product_again_increases_the_quantity()
    {
        await _service.AddItemAsync(7, new AddCartItemRequest { ProductId = 1, Quantity = 1 });
        var cart = await _service.AddItemAsync(7, new AddCartItemRequest { ProductId = 1, Quantity = 2 });

        Assert.Equal(3, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task Cart_total_follows_a_catalog_price_change()
    {
        await _service.AddItemAsync(7, new AddCartItemRequest { ProductId = 1, Quantity = 2 });

        _catalog.Products[1] = new CatalogProduct(1, "Trail Shoe", 120m, true);

        var cart = await _service.GetCartAsync(7);

        Assert.Equal(240m, cart.TotalAmount);
    }

    [Fact]
    public async Task Unknown_and_inactive_products_cannot_be_added()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddItemAsync(7, new AddCartItemRequest { ProductId = 99, Quantity = 1 }));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.AddItemAsync(7, new AddCartItemRequest { ProductId = 3, Quantity = 1 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1001)]
    public async Task Invalid_quantities_are_rejected(int quantity)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.AddItemAsync(7, new AddCartItemRequest { ProductId = 1, Quantity = quantity }));
    }

    [Fact]
    public async Task Quantity_updates_remove_and_clear_work_and_carts_are_per_user()
    {
        await _service.AddItemAsync(7, new AddCartItemRequest { ProductId = 1, Quantity = 1 });
        await _service.AddItemAsync(8, new AddCartItemRequest { ProductId = 1, Quantity = 5 });

        var updated = await _service.UpdateItemAsync(7, 1, 4);

        Assert.Equal(400m, updated.TotalAmount);
        Assert.Equal(500m, (await _service.GetCartAsync(8)).TotalAmount);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateItemAsync(7, 1, 0));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateItemAsync(7, 55, 1));

        var afterRemove = await _service.RemoveItemAsync(7, 1);

        Assert.Empty(afterRemove.Items);
        Assert.Equal(0m, afterRemove.TotalAmount);

        await _service.ClearAsync(8);

        Assert.Empty((await _service.GetCartAsync(8)).Items);
    }

    [Fact]
    public async Task Cart_still_loads_with_saved_prices_when_the_catalog_is_down()
    {
        await _service.AddItemAsync(7, new AddCartItemRequest { ProductId = 1, Quantity = 2 });

        _catalog.Unavailable = true;

        var cart = await _service.GetCartAsync(7);

        Assert.Equal(200m, cart.TotalAmount);
        Assert.Equal("Product #1", cart.Items.Single().ProductName);
    }
}
