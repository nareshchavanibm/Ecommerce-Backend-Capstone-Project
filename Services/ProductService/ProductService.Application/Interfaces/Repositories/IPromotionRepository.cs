using ProductService.Domain.Entities;

namespace ProductService.Application.Interfaces.Repositories
{
    public interface IPromotionRepository
    {
        Task<Promotion?> GetByIdAsync(int id);

        Task<List<Promotion>> GetAllAsync();

        Task<List<Promotion>> GetActiveAsync(DateTime utcNow);

        Task AddAsync(Promotion promotion);

        Task UpdateAsync(Promotion promotion);

        Task DeleteAsync(Promotion promotion);
    }
}
