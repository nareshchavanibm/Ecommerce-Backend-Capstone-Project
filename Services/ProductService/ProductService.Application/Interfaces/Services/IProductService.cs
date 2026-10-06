using ProductService.Application.DTOs;
using ProductService.Application.DTOs.Products;

namespace ProductService.Application.Interfaces.Services
{
    public interface IProductService
    {
        Task<ProductResponseDto?> GetByIdAsync(int id);

        Task<List<ProductResponseDto>> GetAllAsync(
            string? search = null,
            string? category = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? sortBy = null,
            string? sortOrder = null);

        Task<ProductResponseDto> CreateAsync(ProductCreateDto request);

        Task<ProductResponseDto?> UpdateAsync(
            int id,
            ProductCreateDto request);

        Task<List<ProductResponseDto>> BulkUpdateAsync(
            List<BulkUpdateProductRequest> requests);

        Task<bool> DeleteAsync(int id);

        Task<PagedProductResponseDto> GetPagedAsync(
    int pageNumber,
    int pageSize,
    string? search = null,
    string? category = null,
    decimal? minPrice = null,
    decimal? maxPrice = null,
    string? sortBy = null,
    string? sortOrder = null);
    }
}