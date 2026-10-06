using ProductService.Domain.Entities;

namespace ProductService.Application.Interfaces.Repositories
{
    public interface IInventoryRepository
    {
        Task<Inventory?> GetByProductIdAsync(int productId);

        Task<List<Inventory>> GetAllAsync();

        Task AddAsync(Inventory inventory);

        Task UpdateAsync(Inventory inventory);
    }
}