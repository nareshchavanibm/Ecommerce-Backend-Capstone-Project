using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces.Repositories
{
    public interface ICartItemRepository
    {
        Task<CartItem?> GetByIdAsync(int id);
        Task<IEnumerable<CartItem>> GetByCartIdAsync(int cartId);
        Task<CartItem> AddAsync(CartItem cartItem);
        Task UpdateAsync(CartItem cartItem);
        Task DeleteAsync(CartItem cartItem);
    }
}