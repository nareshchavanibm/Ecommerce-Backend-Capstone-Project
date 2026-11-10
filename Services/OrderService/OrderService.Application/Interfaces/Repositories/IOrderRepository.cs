using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces.Repositories
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(int id);
        Task<Order?> GetByOrderNumberAsync(string orderNumber);
        Task<IEnumerable<Order>> GetByUserIdAsync(int userId);
        Task<IEnumerable<Order>> GetAllAsync();
        Task<IReadOnlyList<Order>> GetPendingCreatedBeforeAsync(DateTime cutoffUtc, int take);

        // One statement, so only one caller wins when several try to cancel the same order.
        Task<bool> TryCancelPendingAsync(int orderId);
        Task<Order> AddAsync(Order order);
        Task UpdateAsync(Order order);
    }
}