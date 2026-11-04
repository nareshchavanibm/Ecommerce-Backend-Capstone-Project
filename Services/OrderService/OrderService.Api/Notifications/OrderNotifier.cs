using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Hubs;
using OrderService.Application.DTOs.Orders;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Api.Notifications
{
    public interface IOrderNotifier
    {
        // To admins: a customer placed an order.
        Task OrderCreatedAsync(OrderResponseDto order);

        // To admins: a customer cancelled an order.
        Task OrderCancelledAsync(OrderResponseDto order);

        // To the customer: their order moved to a new status.
        Task OrderStatusChangedAsync(OrderResponseDto order);

        // To admins: a payment succeeded.
        Task OrderPaidAsync(OrderResponseDto order);

        // To every customer waiting for the product; their requests are then removed.
        Task ProductRestockedAsync(int productId);
    }

    public record NotificationDto(int Id, string Title, string Message, string Link, DateTime CreatedAt, bool Read);

    public class OrderNotifier : IOrderNotifier
    {
        private readonly IHubContext<NotificationHub> _hub;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<OrderNotifier> _logger;

        public OrderNotifier(
            IHubContext<NotificationHub> hub,
            IServiceScopeFactory scopes,
            ILogger<OrderNotifier> logger)
        {
            _hub = hub;
            _scopes = scopes;
            _logger = logger;
        }

        public Task OrderCreatedAsync(OrderResponseDto order)
        {
            var items = order.Items.Sum(item => item.Quantity);

            return NotifyAsync(null, order, "New order",
                $"Order {order.OrderNumber} for {Money(order.TotalAmount)} ({items} {(items == 1 ? "item" : "items")}).",
                $"/admin/orders/details/{order.OrderNumber}");
        }

        public Task OrderCancelledAsync(OrderResponseDto order) =>
            NotifyAsync(null, order, "Order cancelled",
                $"Order {order.OrderNumber} was cancelled.",
                $"/admin/orders/details/{order.OrderNumber}");

        public Task OrderStatusChangedAsync(OrderResponseDto order) =>
            NotifyAsync(order.UserId, order, "Order update",
                $"Your order {order.OrderNumber} is now {order.Status}.",
                $"/orders/{order.OrderNumber}");

        public Task OrderPaidAsync(OrderResponseDto order) =>
            NotifyAsync(null, order, "Payment received",
                $"Order {order.OrderNumber} was paid ({Money(order.TotalAmount)}).",
                $"/admin/orders/details/{order.OrderNumber}");

        public async Task ProductRestockedAsync(int productId)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var alerts = await db.StockAlerts.Where(a => a.ProductId == productId).ToListAsync();

            if (alerts.Count == 0)
            {
                return;
            }

            // OrderId is not meaningful for stock notifications.
            var notifications = alerts.Select(alert => new Notification
            {
                UserId = alert.UserId,
                OrderId = 0,
                Title = "Back in stock",
                Message = Truncate($"{alert.ProductName} is available again.", 300),
                Link = $"/products/{productId}"
            }).ToList();

            db.Notifications.AddRange(notifications);
            db.StockAlerts.RemoveRange(alerts);
            await db.SaveChangesAsync();

            foreach (var notification in notifications)
            {
                try
                {
                    await _hub.Clients.Group(NotificationHub.UserGroup(notification.UserId!.Value)).SendAsync(
                        "Notification",
                        new NotificationDto(notification.Id, notification.Title, notification.Message,
                            notification.Link, notification.CreatedAt, false));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not push restock notification {NotificationId}.", notification.Id);
                }
            }
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];

        private static string Money(decimal amount) =>
            "₹" + amount.ToString("N0", CultureInfo.InvariantCulture);

        // Stored first so recipients who are offline still see it later; a failure here must never fail the order request.
        private async Task NotifyAsync(int? userId, OrderResponseDto order, string title, string message, string link)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = userId,
                    OrderId = order.Id,
                    Title = title,
                    Message = message,
                    Link = link
                };

                using (var scope = _scopes.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Notifications.Add(notification);
                    await db.SaveChangesAsync();
                }

                var group = userId is int id ? NotificationHub.UserGroup(id) : NotificationHub.AdminGroup;

                await _hub.Clients.Group(group).SendAsync("Notification",
                    new NotificationDto(notification.Id, title, message, link, notification.CreatedAt, false));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send notification for order {OrderId}.", order.Id);
            }
        }
    }
}
