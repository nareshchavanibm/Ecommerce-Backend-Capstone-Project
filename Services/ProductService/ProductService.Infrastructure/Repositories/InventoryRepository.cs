using Microsoft.EntityFrameworkCore;
using ProductService.Application.DTOs.Inventory;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Data;

namespace ProductService.Infrastructure.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly AppDbContext _context;

        public InventoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Inventory?> GetByProductIdAsync(int productId)
        {
            return await _context.Inventories
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.ProductId == productId);
        }

        public async Task<List<Inventory>> GetAllAsync()
        {
            return await _context.Inventories
                .Include(x => x.Product)
                .ToListAsync();
        }

        public async Task AddAsync(Inventory inventory)
        {
            await _context.Inventories.AddAsync(inventory);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(List<Inventory> inventories)
        {
            await _context.Inventories.AddRangeAsync(inventories);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Inventory inventory)
        {
            _context.Inventories.Update(inventory);
            await _context.SaveChangesAsync();
        }

        public async Task<StockReservationResult> ReserveAsync(
            IReadOnlyCollection<StockLineDto> lines)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Fixed order avoids deadlocks between concurrent reservations.
            foreach (var line in lines.OrderBy(x => x.ProductId))
            {
                // The condition and the deduction are one statement, so stock cannot go negative.
                var updated = await _context.Inventories
                    .Where(x => x.ProductId == line.ProductId && x.Quantity >= line.Quantity)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        x => x.Quantity,
                        x => x.Quantity - line.Quantity));

                if (updated == 0)
                {
                    await transaction.RollbackAsync();

                    var inventory = await _context.Inventories
                        .AsNoTracking()
                        .Include(x => x.Product)
                        .FirstOrDefaultAsync(x => x.ProductId == line.ProductId);

                    return inventory == null
                        ? StockReservationResult.Failed(
                            $"Inventory was not found for product {line.ProductId}.")
                        : StockReservationResult.Failed(
                            $"Insufficient stock for {inventory.Product?.Name ?? $"product {line.ProductId}"}. " +
                            $"Available: {inventory.Quantity}.");
                }
            }

            await transaction.CommitAsync();

            return StockReservationResult.Ok();
        }

        public async Task ReleaseAsync(IReadOnlyCollection<StockLineDto> lines)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            foreach (var line in lines.OrderBy(x => x.ProductId))
            {
                await _context.Inventories
                    .Where(x => x.ProductId == line.ProductId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        x => x.Quantity,
                        x => x.Quantity + line.Quantity));
            }

            await transaction.CommitAsync();
        }
    }
}
