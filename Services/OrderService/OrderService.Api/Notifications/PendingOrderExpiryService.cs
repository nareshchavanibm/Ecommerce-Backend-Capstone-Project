using OrderService.Application.Interfaces.Services;

namespace OrderService.Api.Notifications
{
    // Orders abandoned before payment would otherwise hold their stock forever.
    public sealed class PendingOrderExpiryService(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        ILogger<PendingOrderExpiryService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var minutes = configuration.GetValue("Orders:PendingExpiryMinutes", 60);

            // Zero turns the expiry off.
            if (minutes <= 0)
            {
                return;
            }

            var maxAge = TimeSpan.FromMinutes(minutes);

            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    using var scope = scopes.CreateScope();

                    var expired = await scope.ServiceProvider
                        .GetRequiredService<IOrderService>()
                        .ExpireStaleOrdersAsync(maxAge);

                    if (expired > 0)
                    {
                        logger.LogInformation("Expired {Count} pending orders older than {Minutes} minutes.", expired, minutes);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Pending order expiry run failed; it will run again.");
                }
            }
            while (await WaitAsync(timer, stoppingToken));
        }

        private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                return await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
