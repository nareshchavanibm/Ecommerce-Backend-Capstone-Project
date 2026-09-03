using ECommerce.Application.DTOs.Orders;

namespace ECommerce.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto?> GetByIdAsync(int id);

    Task<List<OrderDto>> GetMyOrdersAsync(
        int userId);

    Task<OrderDto> CreateAsync(
        int userId,
        CreateOrderDto dto);

    Task<List<OrderDto>> GetAllAsync();

    Task<OrderDto> UpdateStatusAsync(
        int orderId,
        string status);

    Task<OrderDto> CancelOrderAsync(
    int orderId,
    int userId,
    bool isAdmin);
}