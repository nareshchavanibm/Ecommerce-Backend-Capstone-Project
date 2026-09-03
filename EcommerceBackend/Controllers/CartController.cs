using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommerce.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CartController(
    ICartService cartService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart()
    {
        var userId = GetUserId();

        if (userId == null)
            return Unauthorized();

        var cart = await cartService.GetCartAsync(userId.Value);

        return Ok(cart);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(
        AddCartItemDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
            return Unauthorized();

        try
        {
            var cart = await cartService.AddItemAsync(
                userId.Value,
                dto);

            return Ok(cart);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("items/{productId}")]
    public async Task<ActionResult<CartDto>> UpdateItem(
        int productId,
        UpdateCartItemDto dto)
    {
        var userId = GetUserId();

        if (userId == null)
            return Unauthorized();

        try
        {
            var cart = await cartService.UpdateItemAsync(
                userId.Value,
                productId,
                dto);

            return Ok(cart);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpDelete("items/{productId}")]
    public async Task<ActionResult<CartDto>> RemoveItem(
        int productId)
    {
        var userId = GetUserId();

        if (userId == null)
            return Unauthorized();

        try
        {
            var cart = await cartService.RemoveItemAsync(
                userId.Value,
                productId);

            return Ok(cart);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var userId = GetUserId();

        if (userId == null)
            return Unauthorized();

        await cartService.ClearCartAsync(userId.Value);

        return NoContent();
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