using ECommerce.Application.DTOs.Products;
using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.BackgroundServices;

public class ProductCacheWarmupService : BackgroundService
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductCacheWarmupService> _logger;

    public ProductCacheWarmupService(
        IProductService productService,
        ILogger<ProductCacheWarmupService> logger)
    {
        _productService = productService;
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

                await _productService.GetFilteredAsync(query);

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
