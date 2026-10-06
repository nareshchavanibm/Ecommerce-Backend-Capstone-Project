using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using OrderService.Application.DTOs.Cart;
using OrderService.Application.Interfaces.Services;

namespace OrderService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

    public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var userId = GetUserId();

            var cart = await _cartService.GetCartAsync(userId);

            return Ok(cart);
        }

        [HttpPost("items")]
        public async Task<IActionResult> AddItem(
            [FromBody] CartItemDto dto)
        {
            var userId = GetUserId();

            var cart = await _cartService.AddItemAsync(
                userId,
                dto);

            return Ok(cart);
        }

        [HttpPut("items/{productId:int}")]
        public async Task<IActionResult> UpdateItem(
            int productId,
            [FromQuery] int quantity)
        {
            var userId = GetUserId();

            var cart = await _cartService.UpdateItemAsync(
                userId,
                productId,
                quantity);

            return Ok(cart);
        }

        [HttpDelete("items/{productId:int}")]
        public async Task<IActionResult> RemoveItem(
            int productId)
        {
            var userId = GetUserId();

            await _cartService.RemoveItemAsync(
                userId,
                productId);

            return NoContent();
        }

        private int GetUserId()
        {
            var userIdValue =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException(
                    "User ID is missing from the authentication token.");
            }

            return userId;
        }
    }


}
