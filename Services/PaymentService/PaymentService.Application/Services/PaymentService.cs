using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _repository;

    public PaymentService(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request)
    {
        var payment = new Payment
        {
            OrderId = request.OrderId,
            UserId = request.UserId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var createdPayment = await _repository.CreateAsync(payment);

        return MapToResponse(createdPayment);
    }

    public async Task<PaymentResponse?> GetPaymentByIdAsync(int id)
    {
        var payment = await _repository.GetByIdAsync(id);

        return payment == null
            ? null
            : MapToResponse(payment);
    }

    public async Task<IEnumerable<PaymentResponse>> GetPaymentsByOrderIdAsync(
        int orderId)
    {
        var payments = await _repository.GetByOrderIdAsync(orderId);

        return payments.Select(MapToResponse);
    }

    public async Task<PaymentResponse?> UpdatePaymentStatusAsync(
        int id,
        PaymentStatus status)
    {
        var payment = await _repository.UpdateStatusAsync(id, status);

        return payment == null
            ? null
            : MapToResponse(payment);
    }

    private static PaymentResponse MapToResponse(Payment payment)
    {
        return new PaymentResponse
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            UserId = payment.UserId,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            TransactionId = payment.TransactionId,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt
        };
    }
}