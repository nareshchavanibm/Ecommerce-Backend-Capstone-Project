using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces.Repositories
{
    public interface IOrderItemRepository
    {
        Task<OrderItem> AddAsync(OrderItem orderItem);
        Task<IEnumerable<OrderItem>> GetByOrderIdAsync(int orderId);

        // Best-selling products since the given time, excluding cancelled orders.
        Task<IReadOnlyList<(int ProductId, int UnitsSold)>> GetTopSellingAsync(DateTime sinceUtc, int take);

        // True when the user has an order containing the product that has been delivered.
        Task<bool> HasDeliveredAsync(int userId, int productId);
    }
}