using System.Text.Json.Serialization;

namespace OrderService.Application.DTOs.Orders
{
    public class OrderResponseDto
    {
        // Internal only: customers and admins identify an order by OrderNumber.
        [JsonIgnore]
        public int Id { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public int UserId { get; set; }

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<OrderItemResponseDto> Items { get; set; } = new();
    }

    public class OrderItemResponseDto
    {
        [JsonIgnore]
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        // Same as UnitPrice; the storefront reads this name.
        public decimal Price => UnitPrice;

        public decimal TotalPrice => UnitPrice * Quantity;
    }
}
