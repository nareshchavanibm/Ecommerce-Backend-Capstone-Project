using PaymentService.Application.DTOs;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

public interface IPaymentService
{
    // The amount is read from the order, never from the caller.
    Task<PaymentResponse> ProcessPaymentAsync(int userId, int orderId, string paymentMethod);

    Task<PaymentResponse?> GetPaymentByIdAsync(int id);

    Task<IEnumerable<PaymentResponse>> GetPaymentsByOrderIdAsync(int orderId);

    Task<PaymentResponse?> UpdatePaymentStatusAsync(
        int id,
        PaymentStatus status);
}
