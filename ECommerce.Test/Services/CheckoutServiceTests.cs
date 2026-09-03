using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Application.Services;
using Moq;

namespace ECommerce.Tests.Services;

public class CheckoutServiceTests
{
    [Fact]
    public async Task CheckoutAsync_ShouldThrow_WhenCartIsEmpty()
    {
        // Arrange

        var cartRepository =
            new Mock<ICartRepository>();

        var orderRepository =
            new Mock<IOrderRepository>();

        var inventoryRepository =
            new Mock<IInventoryRepository>();

        var transactionService =
            new Mock<ITransactionService>();

        transactionService
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(operation => operation());

        cartRepository
            .Setup(x => x.GetByUserIdAsync(1005))
            .ReturnsAsync(
                (ECommerce.Domain.Entities.Cart?)null);

        var checkoutService =
            new CheckoutService(
                cartRepository.Object,
                orderRepository.Object,
                inventoryRepository.Object,
                transactionService.Object);

        // Act & Assert

        await Assert.ThrowsAsync<ArgumentException>(
            () => checkoutService.CheckoutAsync(1005));
    }


    [Fact]
    public async Task CheckoutAsync_ShouldThrow_WhenInventoryIsInsufficient()
    {
        // Arrange

        var cartRepository =
            new Mock<ICartRepository>();

        var orderRepository =
            new Mock<IOrderRepository>();

        var inventoryRepository =
            new Mock<IInventoryRepository>();

        var transactionService =
            new Mock<ITransactionService>();

        transactionService
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(operation => operation());

        var product =
            new ECommerce.Domain.Entities.Product
            {
                Id = 1,
                Name = "Gaming Laptop",
                Price = 75000
            };

        var cart =
            new ECommerce.Domain.Entities.Cart
            {
                Id = 1,
                UserId = 1005
            };

        cart.CartItems.Add(
            new ECommerce.Domain.Entities.CartItem
            {
                Id = 1,
                CartId = 1,
                ProductId = 1,
                Quantity = 5,
                Product = product
            });

        var inventory =
            new ECommerce.Domain.Entities.Inventory
            {
                Id = 1,
                ProductId = 1,
                Quantity = 2,
                ReorderLevel = 1,
                Product = product
            };

        cartRepository
            .Setup(x => x.GetByUserIdAsync(1005))
            .ReturnsAsync(cart);

        inventoryRepository
            .Setup(x => x.GetByProductIdAsync(1))
            .ReturnsAsync(inventory);

        var checkoutService =
            new CheckoutService(
                cartRepository.Object,
                orderRepository.Object,
                inventoryRepository.Object,
                transactionService.Object);

        // Act & Assert

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => checkoutService.CheckoutAsync(1005));

        Assert.Contains(
            "Insufficient stock",
            exception.Message);
    }


    [Fact]
    public async Task CheckoutAsync_ShouldThrow_WhenCartQuantityIsInvalid()
    {
        // Arrange

        var cartRepository =
            new Mock<ICartRepository>();

        var orderRepository =
            new Mock<IOrderRepository>();

        var inventoryRepository =
            new Mock<IInventoryRepository>();

        var transactionService =
            new Mock<ITransactionService>();

        transactionService
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(operation => operation());

        var product =
            new ECommerce.Domain.Entities.Product
            {
                Id = 1,
                Name = "Gaming Laptop",
                Price = 75000
            };

        var cart =
            new ECommerce.Domain.Entities.Cart
            {
                Id = 1,
                UserId = 1005
            };

        cart.CartItems.Add(
            new ECommerce.Domain.Entities.CartItem
            {
                Id = 1,
                CartId = 1,
                ProductId = 1,
                Quantity = 0,
                Product = product
            });

        cartRepository
            .Setup(x => x.GetByUserIdAsync(1005))
            .ReturnsAsync(cart);

        var checkoutService =
            new CheckoutService(
                cartRepository.Object,
                orderRepository.Object,
                inventoryRepository.Object,
                transactionService.Object);

        // Act & Assert

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => checkoutService.CheckoutAsync(1005));

        Assert.Contains(
            "invalid quantity",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task CheckoutAsync_ShouldCreateOrder_ReduceInventory_AndClearCart()
    {
        // Arrange

        var cartRepository =
            new Mock<ICartRepository>();

        var orderRepository =
            new Mock<IOrderRepository>();

        var inventoryRepository =
            new Mock<IInventoryRepository>();

        var transactionService =
            new Mock<ITransactionService>();

        transactionService
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(operation => operation());

        var product =
            new ECommerce.Domain.Entities.Product
            {
                Id = 1,
                Name = "Gaming Laptop",
                Price = 75000
            };

        var cart =
            new ECommerce.Domain.Entities.Cart
            {
                Id = 1,
                UserId = 1005
            };

        cart.CartItems.Add(
            new ECommerce.Domain.Entities.CartItem
            {
                Id = 1,
                CartId = 1,
                ProductId = 1,
                Quantity = 2,
                Product = product
            });

        var inventory =
            new ECommerce.Domain.Entities.Inventory
            {
                Id = 1,
                ProductId = 1,
                Quantity = 10,
                ReorderLevel = 2,
                Product = product
            };

        cartRepository
            .Setup(x => x.GetByUserIdAsync(1005))
            .ReturnsAsync(cart);

        inventoryRepository
            .Setup(x => x.GetByProductIdAsync(1))
            .ReturnsAsync(inventory);

        var checkoutService =
            new CheckoutService(
                cartRepository.Object,
                orderRepository.Object,
                inventoryRepository.Object,
                transactionService.Object);

        // Act

        var result =
            await checkoutService.CheckoutAsync(1005);

        // Assert

        Assert.NotNull(result);

        Assert.Equal(
            150000,
            result.TotalAmount);

        // Inventory should be reduced:
        // 10 - 2 = 8

        Assert.Equal(
            8,
            inventory.Quantity);

        // Cart should be empty

        Assert.Empty(
            cart.CartItems);

        // Order should have been added

        orderRepository.Verify(
            x => x.AddAsync(
                It.Is<ECommerce.Domain.Entities.Order>(
                    order =>
                        order.UserId == 1005 &&
                        order.TotalAmount == 150000 &&
                        order.Status == "Pending" &&
                        order.OrderItems.Count == 1)),
            Times.Once);

        // Inventory update should have been called

        inventoryRepository.Verify(
            x => x.UpdateAsync(inventory),
            Times.Once);

        // Cart update should have been called

        cartRepository.Verify(
            x => x.UpdateAsync(cart),
            Times.Once);

        // Database save should have been called

        //orderRepository.Verify(
        //    x => x.SaveChangesAsync(),
        //    Times.Once);

        // Transaction should have been executed

        transactionService.Verify(
            x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task>>()),
            Times.Once);
    }


    [Fact]
    public async Task CheckoutAsync_ShouldThrow_WhenProductIsNotFound()
    {
        // Arrange

        var cartRepository =
            new Mock<ICartRepository>();

        var orderRepository =
            new Mock<IOrderRepository>();

        var inventoryRepository =
            new Mock<IInventoryRepository>();

        var transactionService =
            new Mock<ITransactionService>();

        transactionService
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(operation => operation());

        var cart =
            new ECommerce.Domain.Entities.Cart
            {
                Id = 1,
                UserId = 1005
            };

        cart.CartItems.Add(
            new ECommerce.Domain.Entities.CartItem
            {
                Id = 1,
                CartId = 1,
                ProductId = 999,
                Quantity = 1,

                // Product does not exist
                Product = null!
            });

        cartRepository
            .Setup(x => x.GetByUserIdAsync(1005))
            .ReturnsAsync(cart);

        var checkoutService =
            new CheckoutService(
                cartRepository.Object,
                orderRepository.Object,
                inventoryRepository.Object,
                transactionService.Object);

        // Act & Assert

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => checkoutService.CheckoutAsync(1005));

        Assert.Contains(
            "Product 999",
            exception.Message);
    }
}