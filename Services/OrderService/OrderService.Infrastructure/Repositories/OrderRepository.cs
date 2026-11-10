using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces.Repositories;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;

        public OrderRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Order?> GetByIdAsync(int id)
        {
            return await _context.Orders
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<Order?> GetByOrderNumberAsync(string orderNumber)
        {
            return await _context.Orders
                .FirstOrDefaultAsync(x => x.OrderNumber == orderNumber);
        }

        public async Task<IEnumerable<Order>> GetByUserIdAsync(int userId)
        {
            return await _context.Orders
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetAllAsync()
        {
            return await _context.Orders
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Order>> GetPendingCreatedBeforeAsync(DateTime cutoffUtc, int take)
        {
            return await _context.Orders
                .Where(x => x.Status == OrderStatus.Pending && x.CreatedAt < cutoffUtc)
                .OrderBy(x => x.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> TryCancelPendingAsync(int orderId)
        {
            var updated = await _context.Orders
                .Where(x => x.Id == orderId && x.Status == OrderStatus.Pending)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, OrderStatus.Cancelled));

            return updated == 1;
        }

        public async Task<Order> AddAsync(Order order)
        {
            // Items attached to the order are saved in the same SaveChanges call.
            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();

            return order;
        }

        public async Task UpdateAsync(Order order)
        {
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
        }
    }
}