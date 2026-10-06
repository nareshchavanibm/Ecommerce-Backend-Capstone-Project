using ProductService.Application.DTOs.Categories;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Application.Interfaces.Services;
using ProductService.Domain.Entities;

namespace ProductService.Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<CategoryResponseDto?> GetByIdAsync(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);

            if (category == null)
                return null;

            return MapToResponse(category);
        }

        public async Task<List<CategoryResponseDto>> GetAllAsync()
        {
            var categories = await _categoryRepository.GetAllAsync();

            return categories
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<CategoryResponseDto> CreateAsync(
            CategoryCreateDto request)
        {
            var exists =
                await _categoryRepository.ExistsByNameAsync(request.Name);

            if (exists)
                throw new InvalidOperationException(
                    "A category with this name already exists.");

            var category = new Category
            {
                Name = request.Name,
                Description = request.Description
            };

            await _categoryRepository.AddAsync(category);

            return MapToResponse(category);
        }

        public async Task<CategoryResponseDto?> UpdateAsync(
            int id,
            CategoryCreateDto request)
        {
            var category = await _categoryRepository.GetByIdAsync(id);

            if (category == null)
                return null;

            var existingCategory =
                await _categoryRepository.ExistsByNameAsync(request.Name);

            if (existingCategory &&
                !string.Equals(
                    category.Name,
                    request.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A category with this name already exists.");
            }

            category.Name = request.Name;
            category.Description = request.Description;

            await _categoryRepository.UpdateAsync(category);

            return MapToResponse(category);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);

            if (category == null)
                return false;

            await _categoryRepository.DeleteAsync(category);

            return true;
        }

        private static CategoryResponseDto MapToResponse(
            Category category)
        {
            return new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                CreatedAt = category.CreatedAt
            };
        }
    }
}