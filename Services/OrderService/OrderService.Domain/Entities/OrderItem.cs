using OrderService.Domain.Common;

namespace OrderService.Domain.Entities
{
    public class OrderItem : BaseEntity
    {
        public int OrderId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public Order? Order { get; set; }
    }
}