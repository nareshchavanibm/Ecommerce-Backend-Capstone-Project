using System.Net;
using System.Net.Http.Json;
using PaymentService.Application.Exceptions;
using PaymentService.Application.Interfaces;

namespace PaymentService.Api.Clients
{
    // Talks to the OrderService's internal endpoints, authenticated with a shared key.
    public sealed class OrderClient(HttpClient httpClient) : IOrderClient
    {
        private sealed record OrderPayload(int Id, string? OrderNumber, int UserId, decimal TotalAmount, string Status);

        private sealed record ErrorPayload(string? Message);

        public Task<OrderSnapshot?> GetOrderAsync(int orderId) =>
            FetchAsync($"internal/orders/{orderId}");

        public Task<OrderSnapshot?> GetOrderByNumberAsync(string orderNumber) =>
            FetchAsync($"internal/orders/by-number/{Uri.EscapeDataString(orderNumber)}");

        private async Task<OrderSnapshot?> FetchAsync(string path)
        {
            try
            {
                using var response = await httpClient.GetAsync(path);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new DependencyUnavailableException(
                        $"The order service returned {(int)response.StatusCode}.");
                }

                var order = await response.Content.ReadFromJsonAsync<OrderPayload>();

                return order == null
                    ? null
                    : new OrderSnapshot(order.Id, order.UserId, order.TotalAmount, order.Status, order.OrderNumber ?? string.Empty);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new DependencyUnavailableException(
                    "The order service is not available right now.", ex);
            }
        }

        public async Task MarkPaidAsync(int orderId)
        {
            try
            {
                using var response = await httpClient.PostAsync($"internal/orders/{orderId}/mark-paid", null);

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var error = await response.Content.ReadFromJsonAsync<ErrorPayload>();

                    throw new BusinessRuleException(error?.Message ?? "The order could not be confirmed.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new DependencyUnavailableException(
                        $"The order service returned {(int)response.StatusCode}.");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new DependencyUnavailableException(
                    "The order service is not available right now.", ex);
            }
        }
    }
}
