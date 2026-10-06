
using ProductService.Domain.Entities;

namespace ProductService.Application.Interfaces.Repositories
{
    public interface ICategoryRepository
    {
        Task<Category?> GetByIdAsync(int id);

        Task<List<Category>> GetByIdsAsync(List<int> ids);

        Task<List<Category>> GetAllAsync();

        Task<bool> ExistsByNameAsync(string name);

        Task AddAsync(Category category);

        Task UpdateAsync(Category category);

        Task DeleteAsync(Category category);
    }
}

