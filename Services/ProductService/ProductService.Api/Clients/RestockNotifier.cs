using System.Net.Http.Json;
using ProductService.Application.Interfaces.Services;

namespace ProductService.Api.Clients
{
    public sealed class RestockNotifier(HttpClient httpClient, ILogger<RestockNotifier> logger) : IRestockNotifier
    {
        // Best effort: a failure here must never fail the stock update.
        public async Task ProductRestockedAsync(int productId)
        {
            try
            {
                using var response = await httpClient.PostAsJsonAsync(
                    "internal/stock-alerts/restocked",
                    new { productId });

                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not send restock notifications for product {ProductId}.", productId);
            }
        }
    }
}
