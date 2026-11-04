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

        // Anonymous, aggregated best sellers for the storefront.
        Task<IReadOnlyList<TrendingProductDto>> GetTrendingProductsAsync(int days, int take);

        // Lets the ReviewService confirm a customer received the product before they review it.
        Task<bool> HasPurchasedAsync(int userId, int productId);

        Task<OrderResponseDto> CancelOrderAsync(string orderNumber, int userId, bool isAdmin);

        Task<OrderResponseDto> UpdateOrderStatusAsync(string orderNumber, string status);

        // Called by the PaymentService once a payment has succeeded.
        Task<OrderResponseDto> MarkPaidAsync(int orderId);
    }
}
