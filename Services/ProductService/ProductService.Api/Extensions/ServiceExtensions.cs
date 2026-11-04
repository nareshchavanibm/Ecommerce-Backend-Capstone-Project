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
            services.AddScoped<IPromotionRepository, PromotionRepository>();

            // Application Services
            services.AddScoped<IProductService, ProductService.Application.Services.ProductService>();
            services.AddScoped<ICategoryService, ProductService.Application.Services.CategoryService>();
            services.AddScoped<IInventoryService, ProductService.Application.Services.InventoryService>();
            services.AddScoped<IPromotionService, ProductService.Application.Services.PromotionService>();

            // Without an OrderService address, restock notifications are simply skipped.
            var orderServiceUrl = configuration["Services:OrderServiceUrl"];
            var internalKey = configuration["Internal:ApiKey"];

            if (!string.IsNullOrWhiteSpace(orderServiceUrl))
            {
                services.AddHttpClient<IRestockNotifier, ProductService.Api.Clients.RestockNotifier>(client =>
                {
                    client.BaseAddress = new Uri(orderServiceUrl);
                    client.Timeout = TimeSpan.FromSeconds(5);

                    if (!string.IsNullOrWhiteSpace(internalKey))
                    {
                        client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
                    }
                });
            }

            return services;
        }
    }
}