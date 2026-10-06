using PaymentService.Application.DTOs;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponse> CreatePaymentAsync(CreatePaymentRequest request);

    Task<PaymentResponse?> GetPaymentByIdAsync(int id);

    Task<IEnumerable<PaymentResponse>> GetPaymentsByOrderIdAsync(int orderId);

    Task<PaymentResponse?> UpdatePaymentStatusAsync(
        int id,
        PaymentStatus status);
}