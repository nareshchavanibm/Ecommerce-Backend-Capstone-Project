using OrderService.Domain.Common;
using OrderService.Domain.Enums;

namespace OrderService.Domain.Entities
{
    public class Order : BaseEntity
    {
        // Customer-facing reference; the integer Id stays internal.
        public string OrderNumber { get; set; } = string.Empty;

        public int UserId { get; set; }

        public decimal TotalAmount { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}