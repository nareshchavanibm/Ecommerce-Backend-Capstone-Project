using ECommerce.Application.DTOs.Payments;

namespace ECommerce.Application.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto?> GetByIdAsync(
        int userId,
        int id);

    Task<PaymentDto?> GetByOrderIdAsync(
        int userId,
        int orderId);

    Task<PaymentDto> ProcessPaymentAsync(
        int userId,
        int orderId,
        CreatePaymentDto dto);
}