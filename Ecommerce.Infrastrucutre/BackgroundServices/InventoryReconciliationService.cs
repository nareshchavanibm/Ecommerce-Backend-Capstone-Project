using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace ECommerce.Infrastructure.BackgroundServices;

public class InventoryReconciliationService : BackgroundService
{
    private readonly IProductRepository _productRepository;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryReconciliationService> _logger;
    private readonly IBackgroundTaskQueue _taskQueue;

    public InventoryReconciliationService(
        IProductRepository productRepository,
        IInventoryService inventoryService,
        IBackgroundTaskQueue taskQueue,
        ILogger<InventoryReconciliationService> logger)
    {
        _productRepository = productRepository;
        _inventoryService = inventoryService;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InventoryReconciliationService starting.");

        var delay = TimeSpan.FromMinutes(60);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var products = await _productRepository.GetAllAsync();

                foreach (var product in products)
                {
                    var id = product.Id;

                    // Enqueue reconciliation work for each product
                    await _taskQueue.QueueBackgroundWorkItemAsync(async ct =>
                    {
                        try
                        {
                            var inv = await _inventoryService.GetByProductIdAsync(id);
                            // Placeholder reconciliation logic: log current inventory
                            _logger.LogInformation("Reconciled product {ProductId}, inventory: {Inventory}", id, inv?.StockQuantity);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error reconciling inventory for product {ProductId}", id);
                        }
                    });
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during inventory reconciliation scheduling.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("InventoryReconciliationService stopping.");
    }
}
