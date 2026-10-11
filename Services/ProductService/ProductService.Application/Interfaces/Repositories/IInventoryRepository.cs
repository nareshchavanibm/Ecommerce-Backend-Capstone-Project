using ProductService.Application.DTOs.Inventory;
using ProductService.Domain.Entities;

namespace ProductService.Application.Interfaces.Repositories
{
    public interface IInventoryRepository
    {
        Task<Inventory?> GetByProductIdAsync(int productId);

        Task<List<Inventory>> GetAllAsync();

        Task AddAsync(Inventory inventory);

        Task AddRangeAsync(List<Inventory> inventories);

        Task UpdateAsync(Inventory inventory);

        // All-or-nothing: either every line is deducted or none is.
        Task<StockReservationResult> ReserveAsync(IReadOnlyCollection<StockLineDto> lines);

        Task ReleaseAsync(IReadOnlyCollection<StockLineDto> lines);
    }
}