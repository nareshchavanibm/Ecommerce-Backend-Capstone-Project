using ECommerce.Application.DTOs.Checkout;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommerce.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CheckoutController(
    ICheckoutService checkoutService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CheckoutDto>> Checkout()
    {
        var userId = GetUserId();

        if (userId == null)
            return Unauthorized();

        try
        {
            var result =
                await checkoutService.CheckoutAsync(
                    userId.Value);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(
            ClaimTypes.NameIdentifier);

        if (claim == null)
            return null;

        if (!int.TryParse(
                claim.Value,
                out var userId))
        {
            return null;
        }

        return userId;
    }
}