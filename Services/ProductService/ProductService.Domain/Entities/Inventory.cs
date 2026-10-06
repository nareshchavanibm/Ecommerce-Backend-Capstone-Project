using ProductService.Domain.Common;

namespace ProductService.Domain.Entities
{
    public class Inventory : BaseEntity
    {
        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public int ReorderLevel { get; set; }

        public Product? Product { get; set; }
    }
}