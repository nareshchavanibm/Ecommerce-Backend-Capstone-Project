namespace ReviewService.Api.Clients
{
    public interface IPurchaseVerifier
    {
        // True when the user has received the product in a delivered order.
        Task<bool> HasPurchasedAsync(int userId, int productId, CancellationToken cancellationToken);
    }

    public sealed class PurchaseCheckUnavailableException(string message, Exception? inner = null)
        : Exception(message, inner);
}
