using ProductService.Application.DTOs.Categories;

namespace ProductService.Application.Interfaces.Services
{
    public interface ICategoryService
    {
        Task<CategoryResponseDto?> GetByIdAsync(int id);

        Task<List<CategoryResponseDto>> GetAllAsync();

        Task<CategoryResponseDto> CreateAsync(
            CategoryCreateDto request);

        Task<CategoryResponseDto?> UpdateAsync(
            int id,
            CategoryCreateDto request);

        Task<bool> DeleteAsync(int id);
    }
}