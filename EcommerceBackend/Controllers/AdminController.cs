using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommerce.Api.Controllers;

[Route("api/admin/orders")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminOrderController(
    IOrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetAll()
    {
        var orders =
            await orderService.GetAllAsync();

        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetById(
        int id)
    {
        var order =
            await orderService.GetByIdAsync(id);

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order was not found."
            });
        }

        return Ok(order);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        int id,
        UpdateOrderStatusDto dto)
    {
        try
        {
            var order =
                await orderService.UpdateStatusAsync(
                    id,
                    dto.Status);

            return Ok(order);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
    [Authorize(Roles = "Admin")]
    [HttpPut("orders/{id}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelOrder(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
            return Unauthorized();

        var userId = int.Parse(userIdClaim.Value);

        try
        {
            var order = await orderService.CancelOrderAsync(
                id,
                userId,
                true);

            return Ok(order);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}