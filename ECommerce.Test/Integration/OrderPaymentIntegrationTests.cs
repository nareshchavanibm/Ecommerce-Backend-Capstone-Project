using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using ECommerce.Infrastructure.Repositories;
using ECommerce.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace ECommerce.Tests.Integration;

public class OrderPaymentIntegrationTests
{
    [Fact]
    public async Task ProcessPayment_Should_SavePayment_And_ConfirmOrder()
    {
        SqlConnection.ClearAllPools();


        // Arrange
        await using var context = TestDbContextFactory.Create();



        var user = new User
        {
            FirstName = "Integration",
            LastName = "Test",
            Email = $"integration_{Guid.NewGuid()}@test.com",
            PasswordHash = "test-password",
            Role = "Customer",
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var order = new Order
        {
            UserId = user.Id,
            OrderDate = DateTime.UtcNow,
            TotalAmount = 75000,
            Status = "Pending"
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var paymentRepository =
            new PaymentRepository(context);

        var orderRepository =
            new OrderRepository(context);

        var unitOfWork =
            new UnitOfWork(context);

        var paymentService =
            new PaymentService(
                paymentRepository,
                orderRepository,
                unitOfWork);

        var dto = new CreatePaymentDto
        {
            PaymentMethod = "CreditCard"
        };

        // Act
        var result =
            await paymentService.ProcessPaymentAsync(
                user.Id,
                order.Id,
                dto);

        // Clear tracked entities so we verify
        // the actual database state.
        context.ChangeTracker.Clear();

        // Assert - Payment
        var databasePayment =
            await context.Payments
                .FirstOrDefaultAsync(
                    p => p.OrderId == order.Id);

        Assert.NotNull(databasePayment);

        Assert.Equal(order.Id, databasePayment!.OrderId);
        Assert.Equal(75000, databasePayment.Amount);
        Assert.Equal("CreditCard", databasePayment.PaymentMethod);
        Assert.Equal("Completed", databasePayment.Status);

        Assert.False(
            string.IsNullOrWhiteSpace(
                databasePayment.TransactionId));

        // Assert - Order
        var databaseOrder =
            await context.Orders
                .FirstOrDefaultAsync(
                    o => o.Id == order.Id);

        Assert.NotNull(databaseOrder);

        Assert.Equal("Confirmed", databaseOrder!.Status);
        Assert.Equal(75000, databaseOrder.TotalAmount);

        // Assert - Service result
        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal(75000, result.Amount);
        Assert.Equal("CreditCard", result.PaymentMethod);
        Assert.Equal("Completed", result.Status);
    }
}