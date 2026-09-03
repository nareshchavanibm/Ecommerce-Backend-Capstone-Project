using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Repositories;

public class InventoryRepository(
    AppDbContext context) : IInventoryRepository
{
    public async Task<Inventory?> GetByProductIdAsync(
        int productId)
    {
        return await context.Inventories
            .FirstOrDefaultAsync(
                x => x.ProductId == productId);
    }

    public Task UpdateAsync(Inventory inventory)
    {
        context.Inventories.Update(inventory);

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}