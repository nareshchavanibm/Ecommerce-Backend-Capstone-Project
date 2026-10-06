using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs.Inventory;
using ProductService.Application.Interfaces.Services;

namespace ProductService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var inventories = await _inventoryService.GetAllAsync();
            return Ok(inventories);
        }

        [HttpGet("{productId:int}")]
        public async Task<IActionResult> GetByProductId(int productId)
        {
            var inventory = await _inventoryService.GetByProductIdAsync(productId);

            if (inventory == null)
                return NotFound(new { message = "Inventory not found for this product." });

            return Ok(inventory);
        }

        [HttpPost("{productId:int}")]
        public async Task<IActionResult> Create(
            int productId,
            [FromBody] InventoryUpdateDto request)
        {
            var inventory = await _inventoryService.CreateAsync(productId, request);

            return CreatedAtAction(
                nameof(GetByProductId),
                new { productId = productId },
                inventory);
        }

        [HttpPut("{productId:int}")]
        public async Task<IActionResult> Update(
            int productId,
            [FromBody] InventoryUpdateDto request)
        {
            var inventory = await _inventoryService.UpdateAsync(productId, request);

            if (inventory == null)
                return NotFound(new { message = "Inventory not found for this product." });

            return Ok(inventory);
        }
    }
}