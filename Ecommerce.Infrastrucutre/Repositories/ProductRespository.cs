
using ECommerce.Application.DTOs.Products;
using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Inventory)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Inventory)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product> AddAsync(Product product)
    {
        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        return product;
    }

    public async Task UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<Product> Products, int TotalCount)> GetFilteredAsync(
        ProductQueryDto query)
    {
        IQueryable<Product> products = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Inventory)
            .AsNoTracking();

        // Search by product name
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            products = products.Where(p =>
                p.Name.Contains(query.Search));
        }

        // Filter by category
        if (query.CategoryId.HasValue)
        {
            products = products.Where(p =>
                p.CategoryId == query.CategoryId.Value);
        }

        // Filter by minimum price
        if (query.MinPrice.HasValue)
        {
            products = products.Where(p =>
                p.Price >= query.MinPrice.Value);
        }

        // Filter by maximum price
        if (query.MaxPrice.HasValue)
        {
            products = products.Where(p =>
                p.Price <= query.MaxPrice.Value);
        }

        // Count BEFORE pagination
        var totalCount = await products.CountAsync();

        // Sorting
        if (!string.IsNullOrWhiteSpace(query.SortBy))
        {
            var descending =
                string.Equals(
                    query.SortOrder,
                    "desc",
                    StringComparison.OrdinalIgnoreCase);

            products = query.SortBy.ToLower() switch
            {
                "price" => descending
                    ? products.OrderByDescending(p => p.Price)
                    : products.OrderBy(p => p.Price),

                "name" => descending
                    ? products.OrderByDescending(p => p.Name)
                    : products.OrderBy(p => p.Name),

                _ => products
            };
        }

        // Pagination
        var skip = (query.PageNumber - 1) * query.PageSize;

        var result = await products
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync();

        return (result, totalCount);
    }

    public async Task DeleteAsync(Product product)
    {
        // Always perform a soft-delete: mark product as inactive
        // This keeps historical relations intact and prevents accidental data loss.
        product.IsActive = false;
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Products
            .AnyAsync(p => p.Id == id);
    }

    public async Task<bool> IsReferencedByOrdersAsync(int productId)
    {
        return await _context.OrderItems.AnyAsync(oi => oi.ProductId == productId);
    }

    public async Task<bool> IsReferencedInCartsAsync(int productId)
    {
        return await _context.CartItems.AnyAsync(ci => ci.ProductId == productId);
    }
}

