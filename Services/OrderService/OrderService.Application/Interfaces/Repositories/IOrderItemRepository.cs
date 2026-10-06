using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces.Repositories
{
    public interface IOrderItemRepository
    {
        Task<OrderItem> AddAsync(OrderItem orderItem);
        Task<IEnumerable<OrderItem>> GetByOrderIdAsync(int orderId);
    }
}