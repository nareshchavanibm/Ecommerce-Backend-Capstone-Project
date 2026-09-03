using ECommerce.Application.DTOs.Payments;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using Moq;

namespace ECommerce.Tests.Services;

public class PaymentServiceTests
{
    private readonly Mock<IPaymentRepository> paymentRepositoryMock;
    private readonly Mock<IOrderRepository> orderRepositoryMock;
    private readonly Mock<IUnitOfWork> unitOfWorkMock;

    private readonly PaymentService paymentService;

    public PaymentServiceTests()
    {
        paymentRepositoryMock =
            new Mock<IPaymentRepository>();

        orderRepositoryMock =
            new Mock<IOrderRepository>();

        unitOfWorkMock =
            new Mock<IUnitOfWork>();

        paymentService = new PaymentService(
            paymentRepositoryMock.Object,
            orderRepositoryMock.Object,
            unitOfWorkMock.Object);
    }


    // =========================================================
    // 1. GetByIdAsync - User owns order
    // =========================================================

    [Fact]
    public async Task GetByIdAsync_Should_ReturnPayment_WhenUserOwnsOrder()
    {
        // Arrange
        var userId = 1;

        var payment = new Payment
        {
            Id = 10,
            OrderId = 100,
            Amount = 75000,
            PaymentMethod = "CreditCard",
            Status = "Completed",
            TransactionId = "TXN-123",
            PaymentDate = DateTime.UtcNow,
            Order = new Order
            {
                Id = 100,
                UserId = userId
            }
        };

        paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(10))
            .ReturnsAsync(payment);

        // Act
        var result =
            await paymentService.GetByIdAsync(
                userId,
                10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Id);
        Assert.Equal(100, result.OrderId);
        Assert.Equal(75000, result.Amount);
        Assert.Equal("CreditCard", result.PaymentMethod);
    }


    // =========================================================
    // 2. GetByIdAsync - User does not own order
    // =========================================================

    [Fact]
    public async Task GetByIdAsync_Should_ThrowUnauthorized_WhenUserDoesNotOwnOrder()
    {
        // Arrange
        var payment = new Payment
        {
            Id = 10,
            OrderId = 100,
            Amount = 75000,
            PaymentMethod = "CreditCard",
            Status = "Completed",
            TransactionId = "TXN-123",
            PaymentDate = DateTime.UtcNow,
            Order = new Order
            {
                Id = 100,
                UserId = 1
            }
        };

        paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(10))
            .ReturnsAsync(payment);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => paymentService.GetByIdAsync(
                2,
                10));
    }


    // =========================================================
    // 3. ProcessPayment - Empty payment method
    // =========================================================

    [Fact]
    public async Task ProcessPaymentAsync_Should_Throw_WhenPaymentMethodIsEmpty()
    {
        // Arrange
        var dto = new CreatePaymentDto
        {
            PaymentMethod = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => paymentService.ProcessPaymentAsync(
                1,
                100,
                dto));
    }


    // =========================================================
    // 4. ProcessPayment - Order not found
    // =========================================================

    [Fact]
    public async Task ProcessPaymentAsync_Should_Throw_WhenOrderNotFound()
    {
        // Arrange
        orderRepositoryMock
            .Setup(r => r.GetByIdAsync(100))
            .ReturnsAsync((Order?)null);

        var dto = new CreatePaymentDto
        {
            PaymentMethod = "CreditCard"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => paymentService.ProcessPaymentAsync(
                1,
                100,
                dto));
    }


    // =========================================================
    // 5. ProcessPayment - User does not own order
    // =========================================================

    [Fact]
    public async Task ProcessPaymentAsync_Should_ThrowUnauthorized_WhenUserDoesNotOwnOrder()
    {
        // Arrange
        var order = new Order
        {
            Id = 100,
            UserId = 1,
            TotalAmount = 75000,
            Status = "Pending"
        };

        orderRepositoryMock
            .Setup(r => r.GetByIdAsync(100))
            .ReturnsAsync(order);

        var dto = new CreatePaymentDto
        {
            PaymentMethod = "CreditCard"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => paymentService.ProcessPaymentAsync(
                2,
                100,
                dto));
    }


    // =========================================================
    // 6. ProcessPayment - Payment already exists
    // =========================================================

    [Fact]
    public async Task ProcessPaymentAsync_Should_Throw_WhenPaymentAlreadyExists()
    {
        // Arrange
        var order = new Order
        {
            Id = 100,
            UserId = 1,
            TotalAmount = 75000,
            Status = "Pending"
        };

        var existingPayment = new Payment
        {
            Id = 10,
            OrderId = 100,
            Amount = 75000,
            PaymentMethod = "CreditCard",
            Status = "Completed",
            TransactionId = "TXN-123",
            PaymentDate = DateTime.UtcNow,
            Order = order
        };

        orderRepositoryMock
            .Setup(r => r.GetByIdAsync(100))
            .ReturnsAsync(order);

        paymentRepositoryMock
            .Setup(r => r.GetByOrderIdAsync(100))
            .ReturnsAsync(existingPayment);

        var dto = new CreatePaymentDto
        {
            PaymentMethod = "CreditCard"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => paymentService.ProcessPaymentAsync(
                1,
                100,
                dto));
    }


    // =========================================================
    // 7. ProcessPayment - Order is not pending
    // =========================================================

    [Fact]
    public async Task ProcessPaymentAsync_Should_Throw_WhenOrderIsNotPending()
    {
        // Arrange
        var order = new Order
        {
            Id = 100,
            UserId = 1,
            TotalAmount = 75000,
            Status = "Confirmed"
        };

        orderRepositoryMock
            .Setup(r => r.GetByIdAsync(100))
            .ReturnsAsync(order);

        paymentRepositoryMock
            .Setup(r => r.GetByOrderIdAsync(100))
            .ReturnsAsync((Payment?)null);

        var dto = new CreatePaymentDto
        {
            PaymentMethod = "CreditCard"
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => paymentService.ProcessPaymentAsync(
                    1,
                    100,
                    dto));

        Assert.Contains(
            "Payment cannot be processed",
            exception.Message);
    }


    // =========================================================
    // 8. ProcessPayment - Successful payment
    // =========================================================

    [Fact]
    public async Task ProcessPaymentAsync_Should_CreatePayment_AndConfirmOrder()
    {
        // Arrange
        var order = new Order
        {
            Id = 100,
            UserId = 1,
            TotalAmount = 75000,
            Status = "Pending"
        };

        orderRepositoryMock
            .Setup(r => r.GetByIdAsync(100))
            .ReturnsAsync(order);

        paymentRepositoryMock
            .Setup(r => r.GetByOrderIdAsync(100))
            .ReturnsAsync((Payment?)null);

        paymentRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync())
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(u => u.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(u => u.CommitTransactionAsync())
            .Returns(Task.CompletedTask);

        var dto = new CreatePaymentDto
        {
            PaymentMethod = "CreditCard"
        };

        // Act
        var result =
            await paymentService.ProcessPaymentAsync(
                1,
                100,
                dto);

        // Assert
        Assert.NotNull(result);

        Assert.Equal(100, result.OrderId);
        Assert.Equal(75000, result.Amount);
        Assert.Equal("CreditCard", result.PaymentMethod);
        Assert.Equal("Completed", result.Status);

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.TransactionId));

        Assert.Equal(
            "Confirmed",
            order.Status);

        paymentRepositoryMock.Verify(
            r => r.AddAsync(
                It.Is<Payment>(p =>
                    p.OrderId == 100 &&
                    p.Amount == 75000 &&
                    p.PaymentMethod == "CreditCard" &&
                    p.Status == "Completed")),
            Times.Once);

        unitOfWorkMock.Verify(
            u => u.BeginTransactionAsync(),
            Times.Once);

        unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(),
            Times.Once);

        unitOfWorkMock.Verify(
            u => u.CommitTransactionAsync(),
            Times.Once);
    }


    // =========================================================
    // 9. ProcessPayment - Rollback when SaveChanges fails
    // =========================================================

    [Fact]
    public async Task ProcessPaymentAsync_Should_Rollback_WhenSaveChangesFails()
    {
        // Arrange
        var order = new Order
        {
            Id = 100,
            UserId = 1,
            TotalAmount = 75000,
            Status = "Pending"
        };

        orderRepositoryMock
            .Setup(r => r.GetByIdAsync(100))
            .ReturnsAsync(order);

        paymentRepositoryMock
            .Setup(r => r.GetByOrderIdAsync(100))
            .ReturnsAsync((Payment?)null);

        paymentRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync())
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(u => u.SaveChangesAsync())
            .ThrowsAsync(
                new Exception("Database error"));

        unitOfWorkMock
            .Setup(u => u.RollbackTransactionAsync())
            .Returns(Task.CompletedTask);

        var dto = new CreatePaymentDto
        {
            PaymentMethod = "CreditCard"
        };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(
            () => paymentService.ProcessPaymentAsync(
                1,
                100,
                dto));

        unitOfWorkMock.Verify(
            u => u.BeginTransactionAsync(),
            Times.Once);

        unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(),
            Times.Once);

        unitOfWorkMock.Verify(
            u => u.RollbackTransactionAsync(),
            Times.Once);

        unitOfWorkMock.Verify(
            u => u.CommitTransactionAsync(),
            Times.Never);
    }
}