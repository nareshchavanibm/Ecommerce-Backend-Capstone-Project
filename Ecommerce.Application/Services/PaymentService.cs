using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;

namespace ECommerce.Application.Services;

public class PaymentService(
    IPaymentRepository paymentRepository,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork) : IPaymentService
{
    public async Task<PaymentDto?> GetByIdAsync(
        int userId,
        int id)
    {
        var payment =
            await paymentRepository.GetByIdAsync(id);

        if (payment == null)
        {
            return null;
        }

        // Make sure the customer owns the order
        if (payment.Order.UserId != userId)
        {
            throw new UnauthorizedAccessException(
                "You are not authorized to view this payment.");
        }

        return MapToDto(payment);
    }

    public async Task<PaymentDto?> GetByOrderIdAsync(
        int userId,
        int orderId)
    {
        var payment =
            await paymentRepository.GetByOrderIdAsync(
                orderId);

        if (payment == null)
        {
            return null;
        }

        // Make sure the customer owns the order
        if (payment.Order.UserId != userId)
        {
            throw new UnauthorizedAccessException(
                "You are not authorized to view this payment.");
        }

        return MapToDto(payment);
    }

    public async Task<PaymentDto> ProcessPaymentAsync(
        int userId,
        int orderId,
        CreatePaymentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PaymentMethod))
        {
            throw new ArgumentException(
                "Payment method is required.");
        }

        var order =
            await orderRepository.GetByIdAsync(orderId);

        if (order == null)
        {
            throw new ArgumentException(
                "Order was not found.");
        }

        // Make sure the customer owns this order
        if (order.UserId != userId)
        {
            throw new UnauthorizedAccessException(
                "You are not authorized to pay for this order.");
        }

        // Check whether payment already exists
        var existingPayment =
            await paymentRepository.GetByOrderIdAsync(
                orderId);

        if (existingPayment != null)
        {
            throw new InvalidOperationException(
                "Payment already exists for this order.");
        }

        if (order.Status != "Pending")
        {
            throw new InvalidOperationException(
                $"Payment cannot be processed for an order " +
                $"with status '{order.Status}'.");
        }

        await unitOfWork.BeginTransactionAsync();

        try
        {
            var payment =
                new Domain.Entities.Payment
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    PaymentMethod =
                        dto.PaymentMethod.Trim(),
                    Status = "Completed",
                    TransactionId =
                        Guid.NewGuid().ToString(),
                    PaymentDate = DateTime.UtcNow
                };

            await paymentRepository.AddAsync(payment);

            // Mock payment succeeded
            order.Status = "Confirmed";

            await unitOfWork.SaveChangesAsync();

            await unitOfWork.CommitTransactionAsync();

            return MapToDto(payment);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync();

            throw;
        }
    }

    private static PaymentDto MapToDto(
        Domain.Entities.Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            Amount = payment.Amount,
            PaymentMethod =
                payment.PaymentMethod,
            Status = payment.Status,
            TransactionId =
                payment.TransactionId,
            PaymentDate =
                payment.PaymentDate
        };
    }
}