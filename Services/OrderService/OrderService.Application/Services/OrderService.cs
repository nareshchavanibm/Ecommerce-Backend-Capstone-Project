using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Exceptions;
using OrderService.Application.Interfaces.Clients;
using OrderService.Application.Interfaces.Repositories;
using OrderService.Application.Interfaces.Services;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;

namespace OrderService.Application.Services
{
    public class OrderService(
        IOrderRepository orderRepository,
        IOrderItemRepository orderItemRepository,
        IProductCatalog productCatalog,
        IStockService stockService,
        ILogger<OrderService> logger) : IOrderService
    {
        private const int MaxItemsPerOrder = 100;

        // No look-alike characters (I, L, O, 0, 1), so numbers are easy to read out.
        private const string NumberAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        private static string NewOrderNumber()
        {
            var chars = new char[8];

            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = NumberAlphabet[RandomNumberGenerator.GetInt32(NumberAlphabet.Length)];
            }

            return "ORD-" + new string(chars);
        }

        public async Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto dto)
        {
            var lines = NormalizeLines(dto.Items);

            var products = await productCatalog.GetProductsAsync(
                lines.Select(x => x.ProductId));

            var orderItems = new List<OrderItem>();

            foreach (var line in lines)
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                {
                    throw new BusinessRuleException($"Product {line.ProductId} was not found.");
                }

                if (!product.IsActive || product.Price <= 0)
                {
                    throw new BusinessRuleException($"{product.Name} is not available to order.");
                }

                // The unit price is the catalog price at this moment, never a client value.
                orderItems.Add(new OrderItem
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = product.Price
                });
            }

            var stockLines = lines
                .Select(x => new StockLine(x.ProductId, x.Quantity))
                .ToList();

            // Stock is held first; BusinessRuleException here means nothing was reserved.
            await stockService.ReserveAsync(stockLines);

            var order = new Order
            {
                OrderNumber = NewOrderNumber(),
                UserId = dto.UserId,
                Status = OrderStatus.Pending,
                TotalAmount = orderItems.Sum(x => x.UnitPrice * x.Quantity),
                OrderItems = orderItems
            };

            try
            {
                await orderRepository.AddAsync(order);
            }
            catch
            {
                await ReleaseQuietlyAsync(stockLines, "order could not be saved");

                throw;
            }

            return Map(order, order.OrderItems, NamesFrom(products));
        }

        public async Task<OrderResponseDto?> GetOrderByIdAsync(int id)
        {
            var order = await orderRepository.GetByIdAsync(id);

            if (order == null)
            {
                return null;
            }

            return (await BuildResponsesAsync([order])).Single();
        }

        public async Task<OrderResponseDto?> GetOrderByNumberAsync(string orderNumber)
        {
            var order = await orderRepository.GetByOrderNumberAsync(orderNumber);

            if (order == null)
            {
                return null;
            }

            return (await BuildResponsesAsync([order])).Single();
        }

        public async Task<IEnumerable<OrderResponseDto>> GetOrdersByUserIdAsync(int userId)
        {
            return await BuildResponsesAsync(
                (await orderRepository.GetByUserIdAsync(userId)).ToList());
        }

        public async Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync()
        {
            return await BuildResponsesAsync(
                (await orderRepository.GetAllAsync()).ToList());
        }

        public async Task<OrderResponseDto> CancelOrderAsync(
            string orderNumber,
            int userId,
            bool isAdmin)
        {
            var order = await orderRepository.GetByOrderNumberAsync(orderNumber)
                ?? throw new NotFoundException("Order not found.");

            if (!isAdmin && order.UserId != userId)
            {
                throw new ForbiddenException("You are not allowed to cancel this order.");
            }

            if (order.Status != OrderStatus.Pending)
            {
                throw new BusinessRuleException(
                    $"Only pending orders can be cancelled (this order is {order.Status}).");
            }

            await SetStatusAsync(order, OrderStatus.Cancelled);

            return (await BuildResponsesAsync([order])).Single();
        }

        public async Task<OrderResponseDto> UpdateOrderStatusAsync(string orderNumber, string status)
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var newStatus) ||
                !Enum.IsDefined(newStatus))
            {
                throw new BusinessRuleException("Invalid order status.");
            }

            var order = await orderRepository.GetByOrderNumberAsync(orderNumber)
                ?? throw new NotFoundException("Order not found.");

            if (order.Status == OrderStatus.Cancelled && newStatus != OrderStatus.Cancelled)
            {
                throw new BusinessRuleException("A cancelled order cannot be reopened.");
            }

            if (order.Status != newStatus)
            {
                await SetStatusAsync(order, newStatus);
            }

            return (await BuildResponsesAsync([order])).Single();
        }

        public async Task<OrderResponseDto> MarkPaidAsync(int orderId)
        {
            var order = await orderRepository.GetByIdAsync(orderId)
                ?? throw new NotFoundException("Order not found.");

            if (order.Status == OrderStatus.Cancelled)
            {
                throw new BusinessRuleException("This order was cancelled.");
            }

            // Idempotent: later stages (shipped, delivered...) are left untouched.
            if (order.Status == OrderStatus.Pending)
            {
                order.Status = OrderStatus.Confirmed;

                await orderRepository.UpdateAsync(order);
            }

            return (await BuildResponsesAsync([order])).Single();
        }

        // Moves to the new status and returns held stock when an order is cancelled.
        private async Task SetStatusAsync(Order order, OrderStatus newStatus)
        {
            order.Status = newStatus;

            await orderRepository.UpdateAsync(order);

            if (newStatus == OrderStatus.Cancelled)
            {
                var items = await orderItemRepository.GetByOrderIdAsync(order.Id);

                await ReleaseQuietlyAsync(
                    items.Select(x => new StockLine(x.ProductId, x.Quantity)).ToList(),
                    $"order {order.OrderNumber} cancelled");
            }
        }

        private async Task ReleaseQuietlyAsync(IReadOnlyCollection<StockLine> lines, string reason)
        {
            try
            {
                await stockService.ReleaseAsync(lines);
            }
            catch (Exception ex)
            {
                // Needs manual correction; the caller's outcome must not change.
                logger.LogError(ex, "Failed to release reserved stock ({Reason}).", reason);
            }
        }

        // Validates input and merges repeated products into one line each.
        private static List<OrderItemCreateDto> NormalizeLines(List<OrderItemCreateDto>? items)
        {
            if (items == null || items.Count == 0)
            {
                throw new BusinessRuleException("At least one order item is required.");
            }

            if (items.Count > MaxItemsPerOrder)
            {
                throw new BusinessRuleException(
                    $"A maximum of {MaxItemsPerOrder} items can be included in an order.");
            }

            if (items.Any(x => x.ProductId <= 0))
            {
                throw new BusinessRuleException("Product ID must be greater than 0.");
            }

            if (items.Any(x => x.Quantity <= 0))
            {
                throw new BusinessRuleException("Quantity must be greater than 0.");
            }

            return items
                .GroupBy(x => x.ProductId)
                .Select(group => new OrderItemCreateDto
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(x => x.Quantity)
                })
                .ToList();
        }

        private async Task<List<OrderResponseDto>> BuildResponsesAsync(List<Order> orders)
        {
            var itemsByOrder = new Dictionary<int, List<OrderItem>>();

            foreach (var order in orders)
            {
                itemsByOrder[order.Id] =
                    (await orderItemRepository.GetByOrderIdAsync(order.Id)).ToList();
            }

            var productIds = itemsByOrder.Values
                .SelectMany(items => items.Select(x => x.ProductId))
                .Distinct()
                .ToList();

            IReadOnlyDictionary<int, string> names = new Dictionary<int, string>();

            if (productIds.Count > 0)
            {
                try
                {
                    names = NamesFrom(await productCatalog.GetProductsAsync(productIds));
                }
                catch (DependencyUnavailableException ex)
                {
                    logger.LogWarning(ex, "Product catalog unavailable while loading order names.");
                }
            }

            return orders
                .Select(order => Map(order, itemsByOrder[order.Id], names))
                .ToList();
        }

        private static IReadOnlyDictionary<int, string> NamesFrom(
            IReadOnlyDictionary<int, CatalogProduct> products)
        {
            return products.ToDictionary(x => x.Key, x => x.Value.Name);
        }

        private static OrderResponseDto Map(
            Order order,
            IEnumerable<OrderItem> items,
            IReadOnlyDictionary<int, string> names)
        {
            var created = DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc);

            return new OrderResponseDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                UserId = order.UserId,
                TotalAmount = order.TotalAmount,
                Status = order.Status.ToString(),
                OrderDate = created,
                CreatedAt = created,
                Items = items.Select(item => new OrderItemResponseDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = names.TryGetValue(item.ProductId, out var name)
                        ? name
                        : $"Product #{item.ProductId}",
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList()
            };
        }
    }
}
