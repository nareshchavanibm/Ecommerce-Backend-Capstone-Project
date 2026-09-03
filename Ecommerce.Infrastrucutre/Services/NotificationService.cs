using ECommerce.Application.DTOs.Products;
using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace ECommerce.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IHubContext<NotificationHub> hubContext, ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyProductCreated(ProductDto product)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ProductCreated", product);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error sending ProductCreated notification.");
        }
    }

    public async Task NotifyProductUpdated(ProductDto product)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ProductUpdated", product);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error sending ProductUpdated notification.");
        }
    }

    public async Task NotifyProductDeleted(int productId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ProductDeleted", productId);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error sending ProductDeleted notification.");
        }
    }
}
