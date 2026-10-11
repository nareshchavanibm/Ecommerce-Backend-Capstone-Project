namespace OrderService.Application.DTOs.Orders
{
    public class OrderCreateDto
    {
        // Set from the authenticated user, never from the request body.
        public int UserId { get; set; }

        public List<OrderItemCreateDto> Items { get; set; } = new();
    }

    // Only ids and quantities are accepted; prices always come from the catalog.
    public class OrderItemCreateDto
    {
        public int ProductId { get; set; }

        public int Quantity { get; set; }
    }

    public class UpdateOrderStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }
}
