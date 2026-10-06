using OrderService.Application.DTOs.Orders;

namespace OrderService.Application.Interfaces.Services
{
    public interface IOrderService
    {
        Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto dto);
        Task<OrderResponseDto?> GetOrderByIdAsync(int id);
        Task<IEnumerable<OrderResponseDto>> GetOrdersByUserIdAsync(int userId);
        Task UpdateOrderStatusAsync(int orderId, int status);
    }
}