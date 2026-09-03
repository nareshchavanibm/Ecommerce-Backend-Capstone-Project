using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);

    Task<List<Order>> GetAllAsync();

    Task<List<Order>> GetByUserIdAsync(int userId);

    Task AddAsync(Order order);

    Task UpdateAsync(Order order);

    Task DeleteAsync(Order order);

    Task SaveChangesAsync();
}