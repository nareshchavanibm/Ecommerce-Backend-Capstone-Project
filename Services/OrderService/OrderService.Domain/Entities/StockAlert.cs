using OrderService.Domain.Common;

namespace OrderService.Domain.Entities
{
    // A customer asked to be told when an out-of-stock product is available again; removed once they are notified.
    public class StockAlert : BaseEntity
    {
        public int UserId { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;
    }
}
