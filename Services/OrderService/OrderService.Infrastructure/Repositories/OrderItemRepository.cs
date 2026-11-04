using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces.Repositories;
using OrderService.Domain.Entities;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repositories
{
    public class OrderItemRepository : IOrderItemRepository
    {
        private readonly AppDbContext _context;

        public OrderItemRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<OrderItem> AddAsync(OrderItem orderItem)
        {
            await _context.OrderItems.AddAsync(orderItem);
            await _context.SaveChangesAsync();

            return orderItem;
        }

        public async Task<IEnumerable<OrderItem>> GetByOrderIdAsync(int orderId)
        {
            return await _context.OrderItems
                .Where(x => x.OrderId == orderId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<(int ProductId, int UnitsSold)>> GetTopSellingAsync(DateTime sinceUtc, int take)
        {
            var rows = await _context.OrderItems
                .AsNoTracking()
                .Where(x => x.Order!.CreatedAt >= sinceUtc && x.Order.Status != OrderStatus.Cancelled)
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, UnitsSold = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.UnitsSold)
                .ThenBy(x => x.ProductId)
                .Take(take)
                .ToListAsync();

            return rows.Select(x => (x.ProductId, x.UnitsSold)).ToList();
        }

        public Task<bool> HasDeliveredAsync(int userId, int productId)
        {
            return _context.OrderItems
                .AsNoTracking()
                .AnyAsync(x => x.ProductId == productId
                    && x.Order!.UserId == userId
                    && x.Order.Status == OrderStatus.Delivered);
        }
    }
}