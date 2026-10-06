namespace OrderService.Application.DTOs.Cart
{
    public class CartResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
    }
}