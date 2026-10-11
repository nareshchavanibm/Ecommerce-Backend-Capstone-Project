using Microsoft.Extensions.Logging;
using PaymentService.Application.DTOs;
using PaymentService.Application.Exceptions;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Services;

public class PaymentService(
    IPaymentRepository repository,
    IOrderClient orderClient,
    IPaymentGateway paymentGateway,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentResponse> ProcessPaymentAsync(
        int userId,
        int orderId,
        string paymentMethod)
    {
        if (orderId <= 0)
        {
            throw new BusinessRuleException("Order ID must be greater than 0.");
        }

        if (!Enum.TryParse<PaymentMethod>(paymentMethod?.Trim(), ignoreCase: true, out var method) ||
            !Enum.IsDefined(method))
        {
            throw new BusinessRuleException("Choose a valid payment method.");
        }

        if (method == PaymentMethod.CashOnDelivery)
        {
            throw new BusinessRuleException(
                "Cash on delivery is not available. Please pay with Card or UPI.");
        }

        var order = await orderClient.GetOrderAsync(orderId)
            ?? throw new NotFoundException("Order not found.");

        if (order.UserId != userId)
        {
            throw new ForbiddenException("You are not allowed to pay for this order.");
        }

        if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                $"Payment cannot be processed for an order with status '{order.Status}'.");
        }

        if ((await repository.GetByOrderIdAsync(orderId)).Any(IsInProgressOrPaid))
        {
            throw new BusinessRuleException("Payment already exists for this order.");
        }

        // The amount owed is the order total held by the OrderService.
        var payment = await repository.CreateAsync(new Payment
        {
            OrderId = orderId,
            UserId = userId,
            Amount = order.TotalAmount,
            PaymentMethod = method,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });

        // Concurrent requests can both pass the check above; the earliest record wins.
        var rivals = (await repository.GetByOrderIdAsync(orderId))
            .Where(other => other.Id < payment.Id && IsInProgressOrPaid(other));

        if (rivals.Any())
        {
            await SetFailedAsync(payment);

            throw new BusinessRuleException("Payment already exists for this order.");
        }

        GatewayResult result;

        try
        {
            result = await paymentGateway.ChargeAsync(payment.Amount, method);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Payment gateway failed for order {OrderId}.", orderId);

            await SetFailedAsync(payment);

            throw new DependencyUnavailableException(
                "The payment provider is not available right now. Please try again.", ex);
        }

        if (!result.Succeeded)
        {
            await SetFailedAsync(payment);

            return Map(payment, order.OrderNumber);
        }

        payment.Status = PaymentStatus.Successful;
        payment.TransactionId = result.TransactionId;
        payment.PaidAt = DateTime.UtcNow;

        await repository.UpdateAsync(payment);

        try
        {
            await orderClient.MarkPaidAsync(orderId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Order {OrderId} could not be confirmed after payment {PaymentId}.", orderId, payment.Id);

            // A real provider integration must also reverse the charge here.
            await SetFailedAsync(payment);

            throw;
        }

        return Map(payment, order.OrderNumber);
    }

    public async Task<PaymentResponse?> GetPaymentByIdAsync(int id)
    {
        var payment = await repository.GetByIdAsync(id);

        return payment == null
            ? null
            : Map(payment);
    }

    public async Task<IEnumerable<PaymentResponse>> GetPaymentsByOrderIdAsync(
        int orderId)
    {
        var payments = await repository.GetByOrderIdAsync(orderId);

        return payments.Select(payment => Map(payment));
    }

    public async Task<PaymentResponse?> UpdatePaymentStatusAsync(
        int id,
        PaymentStatus status)
    {
        var payment = await repository.UpdateStatusAsync(id, status);

        return payment == null
            ? null
            : Map(payment);
    }

    private static bool IsInProgressOrPaid(Payment payment) =>
        payment.Status is PaymentStatus.Pending or PaymentStatus.Successful;

    private async Task SetFailedAsync(Payment payment)
    {
        payment.Status = PaymentStatus.Failed;
        payment.PaidAt = null;

        await repository.UpdateAsync(payment);
    }

    private static PaymentResponse Map(Payment payment, string orderNumber = "")
    {
        var created = DateTime.SpecifyKind(payment.CreatedAt, DateTimeKind.Utc);

        var paid = payment.PaidAt.HasValue
            ? DateTime.SpecifyKind(payment.PaidAt.Value, DateTimeKind.Utc)
            : (DateTime?)null;

        return new PaymentResponse
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            OrderNumber = orderNumber,
            UserId = payment.UserId,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            TransactionId = payment.TransactionId,
            CreatedAt = created,
            PaidAt = paid,
            PaymentDate = paid ?? created
        };
    }
}
