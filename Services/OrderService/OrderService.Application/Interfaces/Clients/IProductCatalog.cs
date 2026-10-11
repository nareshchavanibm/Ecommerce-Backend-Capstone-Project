namespace OrderService.Application.Interfaces.Clients;

public sealed record CatalogProduct(int Id, string Name, decimal Price, bool IsActive);

// Source of truth for product names and prices (the ProductService).
public interface IProductCatalog
{
    // Products that do not exist are simply absent from the result.
    Task<IReadOnlyDictionary<int, CatalogProduct>> GetProductsAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default);
}
