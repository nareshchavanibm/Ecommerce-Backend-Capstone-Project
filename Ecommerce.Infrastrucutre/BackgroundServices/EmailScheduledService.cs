using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace ECommerce.Infrastructure.BackgroundServices;

public class EmailScheduledService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailScheduledService> _logger;

    public EmailScheduledService(IBackgroundTaskQueue taskQueue, IEmailSender emailSender, ILogger<EmailScheduledService> logger)
    {
        _taskQueue = taskQueue;
        _emailSender = emailSender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailScheduledService starting.");

        var delay = TimeSpan.FromMinutes(15);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Example scheduled email task: send a digest to admin
                await _taskQueue.QueueBackgroundWorkItemAsync(async ct =>
                {
                    try
                    {
                        await _emailSender.SendEmailAsync("admin@example.com", "Daily Digest", "This is a scheduled digest.", ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending scheduled email.");
                    }
                });

                _logger.LogInformation("Enqueued scheduled email task.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling emails.");
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

        _logger.LogInformation("EmailScheduledService stopping.");
    }
}
