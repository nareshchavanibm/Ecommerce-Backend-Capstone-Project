namespace ECommerce.Application.DTOs.Orders;

public class OrderDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<OrderItemDto> Items { get; set; } = new();
}