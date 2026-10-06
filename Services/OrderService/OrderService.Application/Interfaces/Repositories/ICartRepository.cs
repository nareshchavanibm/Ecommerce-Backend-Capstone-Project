using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces.Repositories
{
    public interface ICartRepository
    {
        Task<Cart?> GetByUserIdAsync(int userId);
        Task<Cart> AddAsync(Cart cart);
        Task UpdateAsync(Cart cart);
    }
}