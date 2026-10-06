using Microsoft.EntityFrameworkCore;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Application.Interfaces.Services;
using ProductService.Infrastructure.Data;
using ProductService.Infrastructure.Repositories;

namespace ProductService.Api.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddProductService(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")));

            // Repositories
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IInventoryRepository, InventoryRepository>();

            // Application Services
            services.AddScoped<IProductService, ProductService.Application.Services.ProductService>();
            services.AddScoped<ICategoryService, ProductService.Application.Services.CategoryService>();
            services.AddScoped<IInventoryService, ProductService.Application.Services.InventoryService>();

            return services;
        }
    }
}