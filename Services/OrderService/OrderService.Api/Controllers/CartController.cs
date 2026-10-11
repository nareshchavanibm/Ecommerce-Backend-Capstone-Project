using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            return Ok(await _cartService.GetCartAsync(GetUserId()));
        }

        // Prices come from the catalog; only product and quantity are accepted.
        [HttpPost("items")]
        public async Task<IActionResult> AddItem(
            [FromBody] AddCartItemRequest request)
        {
            return Ok(await _cartService.AddItemAsync(GetUserId(), request));
        }

        [HttpPut("items/{productId:int}")]
        public async Task<IActionResult> UpdateItem(
            int productId,
            [FromBody] UpdateCartItemRequest request)
        {
            return Ok(await _cartService.UpdateItemAsync(
                GetUserId(),
                productId,
                request.Quantity));
        }

        [HttpDelete("items/{productId:int}")]
        public async Task<IActionResult> RemoveItem(int productId)
        {
            return Ok(await _cartService.RemoveItemAsync(GetUserId(), productId));
        }

        [HttpDelete]
        public async Task<IActionResult> Clear()
        {
            await _cartService.ClearAsync(GetUserId());

            return NoContent();
        }

        private int GetUserId()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException(
                    "User ID is missing from the authentication token.");
            }

            return userId;
        }
    }
}
