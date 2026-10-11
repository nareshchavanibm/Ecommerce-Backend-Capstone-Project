using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Notifications;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces.Services;

namespace OrderService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IOrderNotifier _notifier;

        public OrderController(IOrderService orderService, IOrderNotifier notifier)
        {
            _orderService = orderService;
            _notifier = notifier;
        }

        // Only ids and quantities are read from the body; the server prices the order.
        [HttpPost]
        public async Task<IActionResult> CreateOrder(
            [FromBody] OrderCreateDto dto)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            dto.UserId = userId;

            var order = await _orderService.CreateOrderAsync(dto);

            await _notifier.OrderCreatedAsync(order);

            return CreatedAtAction(
                nameof(GetOrderByNumber),
                new { orderNumber = order.OrderNumber },
                order);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders()
        {
            return Ok(await _orderService.GetAllOrdersAsync());
        }

        [HttpGet("{orderNumber}")]
        public async Task<IActionResult> GetOrderByNumber(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
            {
                return BadRequest(new { message = "Order number is required." });
            }

            var order = await _orderService.GetOrderByNumberAsync(orderNumber.Trim().ToUpperInvariant());

            if (order == null)
            {
                return NotFound(new { message = "Order not found." });
            }

            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            if (!User.IsInRole("Admin") && order.UserId != userId)
            {
                return Forbid();
            }

            return Ok(order);
        }

        [HttpGet("user/{userId:int}")]
        public async Task<IActionResult> GetOrdersByUserId(int userId)
        {
            if (userId <= 0)
            {
                return BadRequest(new { message = "User ID must be greater than 0." });
            }

            if (!TryGetUserId(out var currentUserId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            if (!User.IsInRole("Admin") && userId != currentUserId)
            {
                return Forbid();
            }

            return Ok(await _orderService.GetOrdersByUserIdAsync(userId));
        }

        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrders()
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            return Ok(await _orderService.GetOrdersByUserIdAsync(userId));
        }

        [HttpPut("{orderNumber}/cancel")]
        public async Task<IActionResult> CancelOrder(string orderNumber)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            var order = await _orderService.CancelOrderAsync(orderNumber.Trim().ToUpperInvariant(), userId, User.IsInRole("Admin"));

            // Tell the other side: admins when a customer cancels, the customer when an admin does.
            if (User.IsInRole("Admin") && order.UserId != userId)
            {
                await _notifier.OrderStatusChangedAsync(order);
            }
            else
            {
                await _notifier.OrderCancelledAsync(order);
            }

            return Ok(order);
        }

        // The storefront sends { "status": "Shipped" }; the numeric query form is still accepted.
        [HttpPut("{orderNumber}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateOrderStatus(
            string orderNumber,
            [FromBody] UpdateOrderStatusDto? body,
            [FromQuery] int? status)
        {
            var requested = !string.IsNullOrWhiteSpace(body?.Status)
                ? body!.Status
                : status?.ToString() ?? string.Empty;

            var order = await _orderService.UpdateOrderStatusAsync(orderNumber.Trim().ToUpperInvariant(), requested);

            await _notifier.OrderStatusChangedAsync(order);

            return Ok(order);
        }

        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
        }
    }
}
