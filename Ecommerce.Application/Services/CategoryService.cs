using ECommerce.Application.DTOs.Categories;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;

namespace ECommerce.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(
        ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }


    public async Task<List<CategoryDto>> GetAllAsync()
    {
        var categories =
            await _categoryRepository.GetAllAsync();

        return categories
            .Select(category => new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
              
                ProductCount = category.Products.Count
            })
            .ToList();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        var category =
            await _categoryRepository.GetByIdAsync(id);

        if (category == null)
        {
            return null;
        }

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,

            ProductCount = category.Products.Count
        };
    }

    public async Task<CategoryDto> CreateAsync(
        CreateCategoryDto dto)
    {
        var category = new Domain.Entities.Category
        {
            Name = dto.Name
        };

        var createdCategory =
            await _categoryRepository.AddAsync(category);

        return new CategoryDto
        {
            Id = createdCategory.Id,
            Name = createdCategory.Name
        };
    }

    public async Task<bool> UpdateAsync(
        int id,
        UpdateCategoryDto dto)
    {
        var category =
            await _categoryRepository.GetByIdAsync(id);

        if (category == null)
        {
            return false;
        }

        category.Name = dto.Name;

        await _categoryRepository.UpdateAsync(category);

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category =
            await _categoryRepository.GetByIdAsync(id);

        if (category == null)
        {
            return false;
        }

        await _categoryRepository.DeleteAsync(category);

        return true;
    }
}
