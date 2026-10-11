using System.Net;
using System.Net.Http.Json;
using OrderService.Application.Exceptions;
using OrderService.Application.Interfaces.Clients;

namespace OrderService.Api.Clients
{
    public sealed class ProductCatalogClient(HttpClient httpClient) : IProductCatalog
    {
        private sealed record ProductPayload(int Id, string Name, decimal Price, bool IsActive);

        public async Task<IReadOnlyDictionary<int, CatalogProduct>> GetProductsAsync(
            IEnumerable<int> productIds,
            CancellationToken cancellationToken = default)
        {
            var ids = productIds.Distinct().ToList();

            var products = await Task.WhenAll(
                ids.Select(id => FetchAsync(id, cancellationToken)));

            return products
                .Where(product => product != null)
                .ToDictionary(product => product!.Id, product => product!);
        }

        private async Task<CatalogProduct?> FetchAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await httpClient.GetAsync($"api/Product/{id}", cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var payload = await response.Content
                    .ReadFromJsonAsync<ProductPayload>(cancellationToken);

                return payload == null
                    ? null
                    : new CatalogProduct(payload.Id, payload.Name, payload.Price, payload.IsActive);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
            {
                throw new DependencyUnavailableException(
                    "The product service is not available right now.", ex);
            }
        }
    }
}
