using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;

namespace ECommerce.Application.Services;

public class OrderService(
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    IInventoryRepository inventoryRepository) : IOrderService
{
    public async Task<OrderDto?> GetByIdAsync(int id)
    {
        var order =
            await orderRepository.GetByIdAsync(id);

        if (order == null)
        {
            return null;
        }

        return MapToDto(order);
    }

    public async Task<List<OrderDto>> GetMyOrdersAsync(
        int userId)
    {
        var orders =
            await orderRepository.GetByUserIdAsync(userId);

        return orders
            .Select(MapToDto)
            .ToList();
    }

    public async Task<OrderDto> CreateAsync(
        int userId,
        CreateOrderDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
        {
            throw new ArgumentException(
                "Order must contain at least one item.");
        }

        var order = new Domain.Entities.Order
        {
            UserId = userId,
            OrderDate = DateTime.UtcNow,
            Status = "Pending"
        };

        foreach (var item in dto.Items)
        {
            if (item.Quantity <= 0)
            {
                throw new ArgumentException(
                    "Quantity must be greater than zero.");
            }

            var product =
                await productRepository.GetByIdAsync(
                    item.ProductId);

            if (product == null)
            {
                throw new ArgumentException(
                    $"Product {item.ProductId} was not found.");
            }

            var inventory =
                await inventoryRepository.GetByProductIdAsync(
                    product.Id);

            if (inventory == null)
            {
                throw new ArgumentException(
                    $"Inventory was not found for {product.Name}.");
            }

            if (inventory.Quantity < item.Quantity)
            {
                throw new ArgumentException(
                    $"Insufficient stock for {product.Name}. " +
                    $"Available: {inventory.Quantity}.");
            }

            inventory.Quantity -= item.Quantity;

            var orderItem =
                new Domain.Entities.OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    TotalPrice =
                        product.Price * item.Quantity
                };

            order.OrderItems.Add(orderItem);
        }

        order.TotalAmount =
            order.OrderItems.Sum(x => x.TotalPrice);

        await orderRepository.AddAsync(order);

        await orderRepository.SaveChangesAsync();

        var createdOrder =
            await orderRepository.GetByIdAsync(order.Id);

        if (createdOrder == null)
        {
            throw new InvalidOperationException(
                "Order was created but could not be retrieved.");
        }

        return MapToDto(createdOrder);
    }

    // ==========================================
    // ADMIN ORDER MANAGEMENT
    // ==========================================

    public async Task<List<OrderDto>> GetAllAsync()
    {
        var orders =
            await orderRepository.GetAllAsync();

        return orders
            .Select(MapToDto)
            .ToList();
    }

    public async Task<OrderDto> UpdateStatusAsync(
        int orderId,
        string status)
    {
        var order =
            await orderRepository.GetByIdAsync(orderId);

        if (order == null)
        {
            throw new ArgumentException(
                "Order was not found.");
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException(
                "Order status is required.");
        }

        var allowedStatuses = new[]
        {
            "Pending",
            "Confirmed",
            "Shipped",
            "Delivered",
            "Cancelled"
        };

        var newStatus = status.Trim();

        if (!allowedStatuses.Contains(
            newStatus,
            StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Invalid order status. " +
                "Allowed values: Pending, Confirmed, " +
                "Shipped, Delivered, Cancelled.");
        }

        // Store the status using the standard casing
        newStatus =
            allowedStatuses.First(
                x => x.Equals(
                    newStatus,
                    StringComparison.OrdinalIgnoreCase));

        order.Status = newStatus;

        await orderRepository.UpdateAsync(order);

        await orderRepository.SaveChangesAsync();

        return MapToDto(order);
    }



    public async Task<OrderDto> CancelOrderAsync(
    int orderId,
    int userId,
    bool isAdmin)
    {
        var order = await orderRepository.GetByIdAsync(orderId);

        if (order == null)
            throw new ArgumentException("Order was not found.");

        // Customer can cancel only their own order
        if (!isAdmin && order.UserId != userId)
            throw new UnauthorizedAccessException(
                "You are not allowed to cancel this order.");

        // Prevent cancelling an already cancelled order
        if (order.Status.Equals(
            "Cancelled",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Order is already cancelled.");
        }

        // Only Pending and Confirmed orders can be cancelled
        if (!order.Status.Equals(
                "Pending",
                StringComparison.OrdinalIgnoreCase)
            &&
            !order.Status.Equals(
                "Confirmed",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Order cannot be cancelled in its current status.");
        }

        // Restore inventory
        foreach (var item in order.OrderItems)
        {
            var inventory =
                await inventoryRepository
                    .GetByProductIdAsync(item.ProductId);

            if (inventory == null)
            {
                throw new InvalidOperationException(
                    $"Inventory not found for product {item.ProductId}.");
            }

            inventory.Quantity += item.Quantity;

            await inventoryRepository.UpdateAsync(inventory);
        }

        // Change order status
        order.Status = "Cancelled";

        await orderRepository.UpdateAsync(order);

        // Save everything
        await orderRepository.SaveChangesAsync();

        return MapToDto(order);
    }




    // ==========================================
    // MAPPING
    // ==========================================

    private static OrderDto MapToDto(
        Domain.Entities.Order order)
    {
        return new OrderDto
        {
            Id = order.Id,

            UserId = order.UserId,

            OrderDate = order.OrderDate,

            TotalAmount = order.TotalAmount,

            Status = order.Status,

            Items = order.OrderItems
                .Select(item => new OrderItemDto
                {
                    ProductId = item.ProductId,

                    ProductName =
                        item.Product.Name,

                    Quantity = item.Quantity,

                    UnitPrice = item.UnitPrice,

                    TotalPrice = item.TotalPrice
                })
                .ToList()
        };
    }
}