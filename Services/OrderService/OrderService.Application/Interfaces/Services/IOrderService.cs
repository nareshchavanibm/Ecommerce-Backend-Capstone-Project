using OrderService.Application.DTOs.Orders;

namespace OrderService.Application.Interfaces.Services
{
    public interface IOrderService
    {
        Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto dto);

        Task<OrderResponseDto?> GetOrderByIdAsync(int id);

        Task<OrderResponseDto?> GetOrderByNumberAsync(string orderNumber);

        Task<IEnumerable<OrderResponseDto>> GetOrdersByUserIdAsync(int userId);

        Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync();

        Task<OrderResponseDto> CancelOrderAsync(string orderNumber, int userId, bool isAdmin);

        Task<OrderResponseDto> UpdateOrderStatusAsync(string orderNumber, string status);

        // Called by the PaymentService once a payment has succeeded.
        Task<OrderResponseDto> MarkPaidAsync(int orderId);
    }
}
