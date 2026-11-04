namespace OrderService.Application.Interfaces.Clients;

// StockQuantity is null when the product has no stock record.
public sealed record CatalogProduct(int Id, string Name, decimal Price, bool IsActive, int? StockQuantity = null);

// Source of truth for product names and prices (the ProductService).
public interface IProductCatalog
{
    // Products that do not exist are simply absent from the result.
    Task<IReadOnlyDictionary<int, CatalogProduct>> GetProductsAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default);
}
