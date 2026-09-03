using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class CartRepository(
    AppDbContext context) : ICartRepository
{
    public async Task<Cart?> GetByUserIdAsync(int userId)
    {
        return await context.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<Cart?> GetByIdAsync(int cartId)
    {
        return await context.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.Id == cartId);
    }

    public async Task AddAsync(Cart cart)
    {
        await context.Carts.AddAsync(cart);
    }

    public Task UpdateAsync(Cart cart)
    {
        context.Carts.Update(cart);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Cart cart)
    {
        context.Carts.Remove(cart);

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}