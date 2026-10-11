using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using System.Security.Claims;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IOrderClient _orderClient;

    public PaymentsController(IPaymentService paymentService, IOrderClient orderClient)
    {
        _paymentService = paymentService;
        _orderClient = orderClient;
    }

    // Pays the order's own total. Only the order number and method come from the caller.
    [HttpPost("orders/{orderNumber}")]
    public async Task<ActionResult<PaymentResponse>> ProcessPayment(
        string orderNumber,
        [FromBody] ProcessPaymentRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid user identity." });
        }

        var order = await _orderClient.GetOrderByNumberAsync(orderNumber.Trim().ToUpperInvariant());

        if (order == null)
        {
            return NotFound(new { message = "Order not found." });
        }

        var payment = await _paymentService.ProcessPaymentAsync(
            userId,
            order.Id,
            request.PaymentMethod);

        return Ok(payment);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentResponse>> GetPaymentById(
        int id)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Payment ID must be greater than 0."
            });
        }

        var payment =
            await _paymentService.GetPaymentByIdAsync(id);

        if (payment == null)
        {
            return NotFound();
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        if (!User.IsInRole("Admin") &&
            payment.UserId != userId)
        {
            return Forbid();
        }

        payment.OrderNumber = (await _orderClient.GetOrderAsync(payment.OrderId))?.OrderNumber ?? string.Empty;

        return Ok(payment);
    }

    [HttpGet("order/{orderNumber}")]
    public async Task<ActionResult<IEnumerable<PaymentResponse>>> GetPaymentsByOrderNumber(
        string orderNumber)
    {
        var order = await _orderClient.GetOrderByNumberAsync(orderNumber.Trim().ToUpperInvariant());

        if (order == null)
        {
            return Ok(Array.Empty<PaymentResponse>());
        }

        var payments =
            (await _paymentService.GetPaymentsByOrderIdAsync(order.Id)).ToList();

        payments.ForEach(payment => payment.OrderNumber = order.OrderNumber);

        if (User.IsInRole("Admin"))
        {
            return Ok(payments);
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        var userPayments = payments
            .Where(payment => payment.UserId == userId)
            .ToList();

        return Ok(userPayments);
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PaymentResponse>> UpdatePaymentStatus(
        int id,
        PaymentStatus status)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Payment ID must be greater than 0."
            });
        }

        var payment =
            await _paymentService.UpdatePaymentStatusAsync(
                id,
                status);

        if (payment == null)
        {
            return NotFound();
        }

        payment.OrderNumber = (await _orderClient.GetOrderAsync(payment.OrderId))?.OrderNumber ?? string.Empty;

        return Ok(payment);
    }

    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}
