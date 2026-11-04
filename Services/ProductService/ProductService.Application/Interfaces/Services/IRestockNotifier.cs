namespace ProductService.Application.Interfaces.Services
{
    // Tells customers waiting for a product that it is back in stock.
    public interface IRestockNotifier
    {
        Task ProductRestockedAsync(int productId);
    }
}
