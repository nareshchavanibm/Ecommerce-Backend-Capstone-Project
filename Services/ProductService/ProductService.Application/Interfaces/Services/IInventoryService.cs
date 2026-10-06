using ProductService.Application.DTOs.Inventory;

namespace ProductService.Application.Interfaces.Services
{
    public interface IInventoryService
    {
        Task<InventoryResponseDto?> GetByProductIdAsync(int productId);

        Task<List<InventoryResponseDto>> GetAllAsync();

        Task<InventoryResponseDto> CreateAsync(
            int productId,
            InventoryUpdateDto request);

        Task<InventoryResponseDto?> UpdateAsync(
            int productId,
            InventoryUpdateDto request);
    }
}