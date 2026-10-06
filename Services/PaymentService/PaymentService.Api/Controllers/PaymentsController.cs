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

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> CreatePayment(
        CreatePaymentRequest request)
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

        request.UserId = userId;

        var payment =
            await _paymentService.CreatePaymentAsync(request);

        return CreatedAtAction(
            nameof(GetPaymentById),
            new { id = payment.Id },
            payment);
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
            payment.UserId != userId)
        {
            return Forbid();
        }

        return Ok(payment);
    }

    [HttpGet("order/{orderId:int}")]
    public async Task<ActionResult<IEnumerable<PaymentResponse>>> GetPaymentsByOrderId(
        int orderId)
    {
        if (orderId <= 0)
        {
            return BadRequest(new
            {
                message = "Order ID must be greater than 0."
            });
        }

        var payments =
            await _paymentService.GetPaymentsByOrderIdAsync(orderId);

        if (User.IsInRole("Admin"))
        {
            return Ok(payments);
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

        return Ok(payment);
    }
}