using Microsoft.AspNetCore.Mvc;
using ProductService.Api.Filters;
using ProductService.Application.DTOs.Inventory;
using ProductService.Application.Interfaces.Services;

namespace ProductService.Api.Controllers
{
    // Called by OrderService only; not routed through the API gateway.
    [ApiController]
    [Route("internal/inventory")]
    [InternalApiKey]
    public class InternalInventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InternalInventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpPost("reserve")]
        public async Task<IActionResult> Reserve(
            [FromBody] StockAdjustmentRequest request)
        {
            var result = await _inventoryService.ReserveAsync(request);

            if (!result.Success)
            {
                return Conflict(new { message = result.Message });
            }

            return Ok();
        }

        [HttpPost("release")]
        public async Task<IActionResult> Release(
            [FromBody] StockAdjustmentRequest request)
        {
            await _inventoryService.ReleaseAsync(request);

            return Ok();
        }
    }
}
