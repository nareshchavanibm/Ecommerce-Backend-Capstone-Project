using ECommerce.Application.DTOs.Inventory;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;

namespace ECommerce.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;

    public InventoryService(
        IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public async Task<InventoryDto?> GetByProductIdAsync(
        int productId)
    {
        var inventory =
            await _inventoryRepository.GetByProductIdAsync(productId);

        if (inventory == null)
        {
            return null;
        }

        return new InventoryDto
        {
            ProductId = inventory.ProductId,
            ProductName = inventory.Product.Name,

            // Your entity property is Quantity
            StockQuantity = inventory.Quantity,

            ReorderLevel = inventory.ReorderLevel,

            IsLowStock =
                inventory.Quantity <= inventory.ReorderLevel
        };
    }

    public async Task<bool> AddStockAsync(
        int productId,
        AddStockDto dto)
    {
        if (dto.Quantity <= 0)
        {
            return false;
        }

        var inventory =
            await _inventoryRepository.GetByProductIdAsync(productId);

        if (inventory == null)
        {
            return false;
        }

        inventory.Quantity += dto.Quantity;

        await _inventoryRepository.UpdateAsync(inventory);

        return true;
    }

    public async Task<bool> RemoveStockAsync(
        int productId,
        RemoveStockDto dto)
    {
        if (dto.Quantity <= 0)
        {
            return false;
        }

        var inventory =
            await _inventoryRepository.GetByProductIdAsync(productId);

        if (inventory == null)
        {
            return false;
        }

        // Prevent negative inventory
        if (inventory.Quantity < dto.Quantity)
        {
            return false;
        }

        inventory.Quantity -= dto.Quantity;

        await _inventoryRepository.UpdateAsync(inventory);

        return true;
    }
}