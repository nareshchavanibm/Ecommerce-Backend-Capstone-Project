using OrderService.Application.Exceptions;
using OrderService.Application.Interfaces.Clients;
using OrderService.Application.Interfaces.Repositories;
using OrderService.Domain.Entities;

namespace Services.Tests;

internal sealed class FakeProductCatalog : IProductCatalog
{
    public Dictionary<int, CatalogProduct> Products { get; } = new();

    public bool Unavailable { get; set; }

    public Task<IReadOnlyDictionary<int, CatalogProduct>> GetProductsAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            throw new DependencyUnavailableException("catalog down");
        }

        IReadOnlyDictionary<int, CatalogProduct> found = productIds
            .Distinct()
            .Where(Products.ContainsKey)
            .ToDictionary(id => id, id => Products[id]);

        return Task.FromResult(found);
    }
}

internal sealed class FakeStockService : IStockService
{
    public List<IReadOnlyCollection<StockLine>> Reserved { get; } = new();

    public List<IReadOnlyCollection<StockLine>> Released { get; } = new();

    public string? RejectWith { get; set; }

    public Task ReserveAsync(IReadOnlyCollection<StockLine> lines)
    {
        if (RejectWith != null)
        {
            throw new BusinessRuleException(RejectWith);
        }

        Reserved.Add(lines);

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(IReadOnlyCollection<StockLine> lines)
    {
        Released.Add(lines);

        return Task.CompletedTask;
    }
}

internal sealed class FakeOrderRepository : IOrderRepository
{
    private int _nextId = 1;

    public List<Order> Orders { get; } = new();

    public bool FailOnAdd { get; set; }

    public Task<Order?> GetByIdAsync(int id) =>
        Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));

    public Task<Order?> GetByOrderNumberAsync(string orderNumber) =>
        Task.FromResult(Orders.FirstOrDefault(x => x.OrderNumber == orderNumber));

    public Task<IEnumerable<Order>> GetByUserIdAsync(int userId) =>
        Task.FromResult(Orders.Where(x => x.UserId == userId));

    public Task<IEnumerable<Order>> GetAllAsync() =>
        Task.FromResult<IEnumerable<Order>>(Orders);

    public Task<IReadOnlyList<Order>> GetPendingCreatedBeforeAsync(DateTime cutoffUtc, int take) =>
        Task.FromResult<IReadOnlyList<Order>>(Orders
            .Where(x => x.Status == OrderService.Domain.Enums.OrderStatus.Pending && x.CreatedAt < cutoffUtc)
            .Take(take)
            .ToList());

    public Task<bool> TryCancelPendingAsync(int orderId)
    {
        var order = Orders.FirstOrDefault(x => x.Id == orderId);

        if (order?.Status != OrderService.Domain.Enums.OrderStatus.Pending)
        {
            return Task.FromResult(false);
        }

        order.Status = OrderService.Domain.Enums.OrderStatus.Cancelled;

        return Task.FromResult(true);
    }

    public Task<Order> AddAsync(Order order)
    {
        if (FailOnAdd)
        {
            throw new InvalidOperationException("database down");
        }

        order.Id = _nextId++;

        foreach (var item in order.OrderItems)
        {
            item.OrderId = order.Id;
        }

        Orders.Add(order);

        return Task.FromResult(order);
    }

    public Task UpdateAsync(Order order) => Task.CompletedTask;
}

internal sealed class FakeOrderItemRepository(FakeOrderRepository orders) : IOrderItemRepository
{
    public Task<OrderItem> AddAsync(OrderItem orderItem) => Task.FromResult(orderItem);

    public Task<IEnumerable<OrderItem>> GetByOrderIdAsync(int orderId) =>
        Task.FromResult<IEnumerable<OrderItem>>(
            orders.Orders.FirstOrDefault(x => x.Id == orderId)?.OrderItems.ToList() ?? new List<OrderItem>());

    public Task<bool> HasDeliveredAsync(int userId, int productId) =>
        Task.FromResult(orders.Orders.Any(o =>
            o.UserId == userId &&
            o.Status == OrderService.Domain.Enums.OrderStatus.Delivered &&
            o.OrderItems.Any(i => i.ProductId == productId)));

    public Task<IReadOnlyList<(int ProductId, int UnitsSold)>> GetTopSellingAsync(DateTime sinceUtc, int take) =>
        Task.FromResult<IReadOnlyList<(int ProductId, int UnitsSold)>>(
            orders.Orders
                .Where(o => o.CreatedAt >= sinceUtc && o.Status != OrderService.Domain.Enums.OrderStatus.Cancelled)
                .SelectMany(o => o.OrderItems)
                .GroupBy(i => i.ProductId)
                .Select(g => (ProductId: g.Key, UnitsSold: g.Sum(i => i.Quantity)))
                .OrderByDescending(x => x.UnitsSold)
                .ThenBy(x => x.ProductId)
                .Take(take)
                .ToList());
}

internal sealed class FakeCartRepository : ICartRepository
{
    private int _nextId = 1;

    public List<Cart> Carts { get; } = new();

    public Task<Cart?> GetByUserIdAsync(int userId) =>
        Task.FromResult(Carts.FirstOrDefault(x => x.UserId == userId));

    public Task<Cart> AddAsync(Cart cart)
    {
        cart.Id = _nextId++;
        Carts.Add(cart);

        return Task.FromResult(cart);
    }

    public Task UpdateAsync(Cart cart) => Task.CompletedTask;
}

internal sealed class FakeCartItemRepository : ICartItemRepository
{
    private int _nextId = 1;

    public List<CartItem> Items { get; } = new();

    public Task<CartItem?> GetByIdAsync(int id) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<IEnumerable<CartItem>> GetByCartIdAsync(int cartId) =>
        Task.FromResult<IEnumerable<CartItem>>(Items.Where(x => x.CartId == cartId).ToList());

    public Task<CartItem> AddAsync(CartItem cartItem)
    {
        cartItem.Id = _nextId++;
        Items.Add(cartItem);

        return Task.FromResult(cartItem);
    }

    public Task UpdateAsync(CartItem cartItem) => Task.CompletedTask;

    public Task DeleteAsync(CartItem cartItem)
    {
        Items.Remove(cartItem);

        return Task.CompletedTask;
    }

    public Task DeleteRangeAsync(IEnumerable<CartItem> cartItems)
    {
        foreach (var item in cartItems.ToList())
        {
            Items.Remove(item);
        }

        return Task.CompletedTask;
    }
}
