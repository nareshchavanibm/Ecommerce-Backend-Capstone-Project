using Microsoft.EntityFrameworkCore;
using OrderService.Api.Clients;
using OrderService.Application.Interfaces.Clients;
using OrderService.Application.Interfaces.Repositories;
using OrderService.Application.Interfaces.Services;
using OrderService.Application.Services;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.Repositories;

namespace OrderService.Api.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddOrderService(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IOrderItemRepository, OrderItemRepository>();
            services.AddScoped<ICartRepository, CartRepository>();
            services.AddScoped<ICartItemRepository, CartItemRepository>();

            services.AddScoped<IOrderService, OrderService.Application.Services.OrderService>();
            services.AddScoped<ICartService, CartService>();

            var productServiceUrl = configuration["Services:ProductServiceUrl"]
                ?? throw new InvalidOperationException(
                    "Services:ProductServiceUrl is not configured.");

            var internalKey = configuration["Internal:ApiKey"];

            services.AddHttpClient<IProductCatalog, ProductCatalogClient>(client =>
            {
                client.BaseAddress = new Uri(productServiceUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            services.AddHttpClient<IStockService, StockServiceClient>(client =>
            {
                client.BaseAddress = new Uri(productServiceUrl);
                client.Timeout = TimeSpan.FromSeconds(10);

                if (!string.IsNullOrWhiteSpace(internalKey))
                {
                    client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
                }
            });

            return services;
        }
    }
}