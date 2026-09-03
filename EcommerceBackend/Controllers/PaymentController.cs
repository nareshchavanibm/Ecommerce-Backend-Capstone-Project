//using System.Security.Claims;
//using ECommerce.Application.DTOs.Payments;
//using ECommerce.Application.Interfaces;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;

//namespace ECommerce.Api.Controllers;

//[Route("api/[controller]")]
//[ApiController]
//[Authorize]
//public class PaymentController(
//    IPaymentService paymentService) : ControllerBase
//{
//    [HttpPost("orders/{orderId}")]
//    public async Task<ActionResult<PaymentDto>> ProcessPayment(
//        int orderId,
//        CreatePaymentDto dto)
//    {
//        var userIdClaim =
//            User.FindFirstValue(
//                ClaimTypes.NameIdentifier);

//        if (!int.TryParse(userIdClaim, out var userId))
//        {
//            return Unauthorized();
//        }

//        try
//        {
//            var payment =
//                await paymentService.ProcessPaymentAsync(
//                    userId,
//                    orderId,
//                    dto);

//            return Ok(payment);
//        }
//        catch (ArgumentException ex)
//        {
//            return BadRequest(new
//            {
//                message = ex.Message
//            });
//        }
//        catch (UnauthorizedAccessException ex)
//        {
//            return Forbid();
//        }
//        catch (InvalidOperationException ex)
//        {
//            return BadRequest(new
//            {
//                message = ex.Message
//            });
//        }
//    }

//    [HttpGet("{id:int}")]
//    public async Task<ActionResult<PaymentDto>> GetById(
//    int id)
//    {
//        var userIdClaim =
//            User.FindFirstValue(
//                ClaimTypes.NameIdentifier);

//        if (!int.TryParse(userIdClaim, out var userId))
//        {
//            return Unauthorized();
//        }

//        try
//        {
//            var payment =
//                await paymentService.GetByIdAsync(
//                    userId,
//                    id);

//            if (payment == null)
//            {
//                return NotFound(new
//                {
//                    message = "Payment was not found."
//                });
//            }

//            return Ok(payment);
//        }
//        catch (UnauthorizedAccessException)
//        {
//            return Forbid();
//        }
//    }

//    [HttpGet("order/{orderId:int}")]

//    public async Task<ActionResult<PaymentDto>> GetByOrderId(
//    int orderId)
//    {
//        var userIdClaim =
//            User.FindFirstValue(
//                ClaimTypes.NameIdentifier);

//        if (!int.TryParse(userIdClaim, out var userId))
//        {
//            return Unauthorized();
//        }

//        try
//        {
//            var payment =
//                await paymentService.GetByOrderIdAsync(
//                    userId,
//                    orderId);

//            if (payment == null)
//            {
//                return NotFound(new
//                {
//                    message =
//                        "Payment was not found for this order."
//                });
//            }

//            return Ok(payment);
//        }
//        catch (UnauthorizedAccessException)
//        {
//            return Forbid();
//        }
//    }
//}


using System.Security.Claims;
using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PaymentController(
    IPaymentService paymentService) : ControllerBase
{
    [HttpPost("orders/{orderId}")]
    public async Task<ActionResult<PaymentDto>> ProcessPayment(
        int orderId,
        CreatePaymentDto dto)
    {
        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var payment =
            await paymentService.ProcessPaymentAsync(
                userId,
                orderId,
                dto);

        return Ok(payment);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentDto>> GetById(
        int id)
    {
        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var payment =
            await paymentService.GetByIdAsync(
                userId,
                id);

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Payment was not found."
            });
        }

        return Ok(payment);
    }

    [HttpGet("order/{orderId:int}")]
    public async Task<ActionResult<PaymentDto>> GetByOrderId(
        int orderId)
    {
        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var payment =
            await paymentService.GetByOrderIdAsync(
                userId,
                orderId);

        if (payment == null)
        {
            return NotFound(new
            {
                message =
                    "Payment was not found for this order."
            });
        }

        return Ok(payment);
    }
}