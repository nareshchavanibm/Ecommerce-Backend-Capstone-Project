using ECommerce.Application.DTOs.Inventory;

namespace ECommerce.Application.Interfaces;

public interface IInventoryService
{
    Task<InventoryDto?> GetByProductIdAsync(int productId);

    Task<bool> AddStockAsync(
        int productId,
        AddStockDto dto);

    Task<bool> RemoveStockAsync(
        int productId,
        RemoveStockDto dto);
}