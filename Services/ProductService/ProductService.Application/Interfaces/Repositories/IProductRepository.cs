using ProductService.Domain.Entities;

namespace ProductService.Application.Interfaces.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id);

        Task<List<Product>> GetByIdsAsync(List<int> ids);

        Task<List<Product>> GetAllAsync(
            string? search = null,
            string? category = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? sortBy = null,
            string? sortOrder = null);

        Task<(List<Product> Products, int TotalItems)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? search = null,
            string? category = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? sortBy = null,
            string? sortOrder = null);

        Task AddAsync(Product product);

        Task UpdateAsync(Product product);

        Task UpdateRangeAsync(List<Product> products);

        Task DeleteAsync(Product product);
    }
}