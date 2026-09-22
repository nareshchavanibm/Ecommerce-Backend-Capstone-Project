using ECommerce.Application.DTOs.Products;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Infrastructure.BackgroundServices;

public class ProductCacheWarmupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductCacheWarmupService> _logger;

    public ProductCacheWarmupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProductCacheWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProductCacheWarmupService starting.");

        // Warmup interval — adjust as needed
        var delay = TimeSpan.FromMinutes(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Warm common product list (first page)
                var query = new ProductQueryDto
                {
                    PageNumber = 1,
                    PageSize = 10
                };

                using var scope = _scopeFactory.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

                await productService.GetFilteredAsync(query);

                _logger.LogInformation("Warmed product list cache (page 1).");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while warming product cache.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // exit loop
                break;
            }
        }

        _logger.LogInformation("ProductCacheWarmupService stopping.");
    }
}
