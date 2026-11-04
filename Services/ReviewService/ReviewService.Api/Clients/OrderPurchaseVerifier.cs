using System.Net.Http.Json;

namespace ReviewService.Api.Clients
{
    // Asks the OrderService's internal endpoint, authenticated with a shared key.
    public sealed class OrderPurchaseVerifier(HttpClient httpClient) : IPurchaseVerifier
    {
        private sealed record PurchasePayload(bool Purchased);

        public async Task<bool> HasPurchasedAsync(int userId, int productId, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await httpClient.GetAsync(
                    $"internal/orders/purchased?userId={userId}&productId={productId}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new PurchaseCheckUnavailableException(
                        $"The order service returned {(int)response.StatusCode}.");
                }

                var payload = await response.Content.ReadFromJsonAsync<PurchasePayload>(cancellationToken);

                return payload?.Purchased ?? false;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new PurchaseCheckUnavailableException("The order service is not available right now.", ex);
            }
        }
    }
}
