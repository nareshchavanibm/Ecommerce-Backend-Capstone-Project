namespace OrderService.Application.DTOs.Orders
{
    public class OrderCreateDto
    {
        public int UserId { get; set; }
        public List<OrderItemCreateDto> Items { get; set; } = new();
    }

    public class OrderItemCreateDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}