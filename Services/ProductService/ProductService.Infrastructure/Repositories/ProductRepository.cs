
using Microsoft.EntityFrameworkCore;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Data;

namespace ProductService.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;

        public ProductRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<Product>> GetByIdsAsync(List<int> ids)
        {
            return await _context.Products
                .Include(x => x.Category)
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();
        }

        public async Task<List<Product>> GetAllAsync(
            string? search = null,
            string? category = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.Name.Contains(search) ||
                    x.Description.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(x =>
                    x.Category != null &&
                    x.Category.Name == category);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(x =>
                    x.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(x =>
                    x.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                bool isDescending =
                    sortOrder?.ToLower() == "desc";

                if (sortBy.ToLower() == "name")
                {
                    if (isDescending)
                        query = query.OrderByDescending(x => x.Name);
                    else
                        query = query.OrderBy(x => x.Name);
                }
                else if (sortBy.ToLower() == "price")
                {
                    if (isDescending)
                        query = query.OrderByDescending(x => x.Price);
                    else
                        query = query.OrderBy(x => x.Price);
                }
            }

            return await query.ToListAsync();
        }

        public async Task<(List<Product> Products, int TotalItems)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? search = null,
            string? category = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.Name.Contains(search) ||
                    x.Description.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(x =>
                    x.Category != null &&
                    x.Category.Name == category);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(x =>
                    x.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(x =>
                    x.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                bool isDescending =
                    sortOrder?.ToLower() == "desc";

                if (sortBy.ToLower() == "name")
                {
                    if (isDescending)
                        query = query.OrderByDescending(x => x.Name);
                    else
                        query = query.OrderBy(x => x.Name);
                }
                else if (sortBy.ToLower() == "price")
                {
                    if (isDescending)
                        query = query.OrderByDescending(x => x.Price);
                    else
                        query = query.OrderBy(x => x.Price);
                }
            }

            var totalItems = await query.CountAsync();

            var products = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (products, totalItems);
        }

        public async Task AddAsync(Product product)
        {
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRangeAsync(List<Product> products)
        {
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Product product)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }
}

