using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces.Services;
using System.Security.Claims;

namespace OrderService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(
            [FromBody] OrderCreateDto dto)
        {
            var userIdClaim = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user identity."
                });
            }

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new
                {
                    message = "At least one order item is required."
                });
            }

            if (dto.Items.Count > 100)
            {
                return BadRequest(new
                {
                    message = "A maximum of 100 items can be included in an order."
                });
            }

            foreach (var item in dto.Items)
            {
                if (item.ProductId <= 0)
                {
                    return BadRequest(new
                    {
                        message = "Product ID must be greater than 0."
                    });
                }

                if (item.Quantity <= 0)
                {
                    return BadRequest(new
                    {
                        message = "Quantity must be greater than 0."
                    });
                }

                if (item.UnitPrice <= 0)
                {
                    return BadRequest(new
                    {
                        message = "Unit price must be greater than 0."
                    });
                }
            }

            dto.UserId = userId;

            var order = await _orderService.CreateOrderAsync(dto);

            return CreatedAtAction(
                nameof(GetOrderById),
                new { id = order.Id },
                order);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Order ID must be greater than 0."
                });
            }

            var order = await _orderService.GetOrderByIdAsync(id);

            if (order == null)
            {
                return NotFound(new
                {
                    message = "Order not found."
                });
            }

            var userIdClaim = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user identity."
                });
            }

            if (!User.IsInRole("Admin") &&
                order.UserId != userId)
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
                return BadRequest(new
                {
                    message = "User ID must be greater than 0."
                });
            }

            var userIdClaim = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var currentUserId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user identity."
                });
            }

            if (!User.IsInRole("Admin") &&
                userId != currentUserId)
            {
                return Forbid();
            }

            var orders =
                await _orderService.GetOrdersByUserIdAsync(userId);

            return Ok(orders);
        }

        [HttpPut("{id:int}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateOrderStatus(
            int id,
            [FromQuery] int status)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Order ID must be greater than 0."
                });
            }

            if (status < 1 || status > 6)
            {
                return BadRequest(new
                {
                    message = "Invalid order status."
                });
            }

            await _orderService.UpdateOrderStatusAsync(id, status);

            return NoContent();
        }
    }
}