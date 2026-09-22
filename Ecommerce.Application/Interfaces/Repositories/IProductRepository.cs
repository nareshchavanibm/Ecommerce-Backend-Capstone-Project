using ECommerce.Application.DTOs.Products;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces.Repositories;

public interface IProductRepository
{
    // Get all products
    Task<List<Product>> GetAllAsync();

    // Get product by ID
    Task<Product?> GetByIdAsync(int id);

    // Add a new product
    Task<Product> AddAsync(Product product);

    // Update an existing product
    Task UpdateAsync(Product product);

    // Delete a product
    Task DeleteAsync(Product product);

    // Check whether a product exists
    Task<bool> ExistsAsync(int id);

    // Check whether product is referenced by orders
    Task<bool> IsReferencedByOrdersAsync(int productId);

    // Check whether product exists in any cart
    Task<bool> IsReferencedInCartsAsync(int productId);

    // Search, filter and sort products (returns paged items and total count)
    Task<(List<Product> Products, int TotalCount)> GetFilteredAsync(ProductQueryDto query);
}