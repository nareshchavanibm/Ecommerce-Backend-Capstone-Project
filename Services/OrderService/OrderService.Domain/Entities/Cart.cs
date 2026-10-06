using OrderService.Domain.Common;

namespace OrderService.Domain.Entities
{
    public class Cart : BaseEntity
    {
        public int UserId { get; set; }

        public ICollection<CartItem> CartItems { get; set; }
            = new List<CartItem>();
    }
}