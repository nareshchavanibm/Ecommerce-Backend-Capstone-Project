using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Filters;
using OrderService.Api.Notifications;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces.Services;

namespace OrderService.Api.Controllers
{
    // Called by PaymentService only; not routed through the API gateway.
    [ApiController]
    [Route("internal/orders")]
    [InternalApiKey]
    public class InternalOrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IOrderNotifier _notifier;

        public InternalOrdersController(IOrderService orderService, IOrderNotifier notifier)
        {
            _orderService = orderService;
            _notifier = notifier;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrder(int id) =>
            Snapshot(await _orderService.GetOrderByIdAsync(id));

        [HttpGet("by-number/{orderNumber}")]
        public async Task<IActionResult> GetOrderByNumber(string orderNumber) =>
            Snapshot(await _orderService.GetOrderByNumberAsync(orderNumber.Trim().ToUpperInvariant()));

        [HttpPost("{id:int}/mark-paid")]
        public async Task<IActionResult> MarkPaid(int id)
        {
            var order = await _orderService.MarkPaidAsync(id);

            await _notifier.OrderPaidAsync(order);
            await _notifier.OrderStatusChangedAsync(order);

            return Ok(order);
        }

        // Unlike the public DTO, this carries the internal id the payment service stores.
        private IActionResult Snapshot(OrderResponseDto? order) =>
            order == null
                ? NotFound(new { message = "Order not found." })
                : Ok(new
                {
                    id = order.Id,
                    orderNumber = order.OrderNumber,
                    userId = order.UserId,
                    totalAmount = order.TotalAmount,
                    status = order.Status
                });
    }
}
