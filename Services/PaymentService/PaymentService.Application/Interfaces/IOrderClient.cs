namespace PaymentService.Application.Interfaces;

public sealed record OrderSnapshot(int Id, int UserId, decimal TotalAmount, string Status, string OrderNumber = "");

// The OrderService is the source of truth for what is owed.
public interface IOrderClient
{
    Task<OrderSnapshot?> GetOrderAsync(int orderId);

    // Customers know an order only by its public number.
    Task<OrderSnapshot?> GetOrderByNumberAsync(string orderNumber);

    // Moves a pending order to Confirmed after a successful payment.
    Task MarkPaidAsync(int orderId);
}
