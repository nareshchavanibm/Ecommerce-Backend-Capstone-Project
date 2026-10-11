using System.Net;
using System.Net.Http.Json;
using OrderService.Application.Exceptions;
using OrderService.Application.Interfaces.Clients;

namespace OrderService.Api.Clients
{
    public sealed class StockServiceClient(HttpClient httpClient) : IStockService
    {
        private sealed record ErrorPayload(string? Message);

        public Task ReserveAsync(IReadOnlyCollection<StockLine> lines) =>
            SendAsync("internal/inventory/reserve", lines);

        public Task ReleaseAsync(IReadOnlyCollection<StockLine> lines) =>
            SendAsync("internal/inventory/release", lines);

        private async Task SendAsync(string path, IReadOnlyCollection<StockLine> lines)
        {
            try
            {
                using var response = await httpClient.PostAsJsonAsync(path, new
                {
                    items = lines.Select(line => new { productId = line.ProductId, quantity = line.Quantity })
                });

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    var error = await response.Content.ReadFromJsonAsync<ErrorPayload>();

                    throw new BusinessRuleException(error?.Message ?? "Insufficient stock.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new DependencyUnavailableException(
                        $"The inventory service returned {(int)response.StatusCode}.");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new DependencyUnavailableException(
                    "The inventory service is not available right now.", ex);
            }
        }
    }
}
