namespace OrderService.Application.Interfaces.Clients;

public sealed record StockLine(int ProductId, int Quantity);

// Inventory held by the ProductService.
public interface IStockService
{
    // Throws BusinessRuleException when any line cannot be fulfilled; nothing is deducted then.
    Task ReserveAsync(IReadOnlyCollection<StockLine> lines);

    Task ReleaseAsync(IReadOnlyCollection<StockLine> lines);
}
