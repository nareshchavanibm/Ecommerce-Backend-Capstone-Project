using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(int userId);

    Task<Cart?> GetByIdAsync(int cartId);

    Task AddAsync(Cart cart);

    Task UpdateAsync(Cart cart);

    Task DeleteAsync(Cart cart);

    Task SaveChangesAsync();
}