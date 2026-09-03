using ECommerce.Application.DTOs.Inventory;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(
        IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("{productId:int}")]
    public async Task<ActionResult<InventoryDto>> Get(
        int productId)
    {
        var inventory =
            await _inventoryService.GetByProductIdAsync(productId);

        if (inventory == null)
        {
            return NotFound();
        }

        return Ok(inventory);
    }

    [HttpPost("{productId:int}/add")]
    public async Task<IActionResult> AddStock(
        int productId,
        AddStockDto dto)
    {
        var result =
            await _inventoryService.AddStockAsync(
                productId,
                dto);

        if (!result)
        {
            return BadRequest(
                "Unable to add stock.");
        }

        return NoContent();
    }

    [HttpPost("{productId:int}/remove")]
    public async Task<IActionResult> RemoveStock(
        int productId,
        RemoveStockDto dto)
    {
        var result =
            await _inventoryService.RemoveStockAsync(
                productId,
                dto);

        if (!result)
        {
            return BadRequest(
                "Unable to remove stock. " +
                "Product may not exist or stock may be insufficient.");
        }

        return NoContent();
    }
}