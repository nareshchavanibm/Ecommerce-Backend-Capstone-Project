using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Data;
using ECommerce.Tests.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Data;

public class TransactionServiceTests
{
    [Fact]
    public async Task Transaction_Should_Rollback_When_Operation_Fails()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create();

        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        var category = new Category
        {
            Name = $"Test Category {Guid.NewGuid()}"
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Name = $"Test Product {Guid.NewGuid()}",
            Description = "Transaction test product",
            Price = 1000,
            CategoryId = category.Id
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();

        var originalPrice = product.Price;

        var transactionService =
            new TransactionService(context);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                await transactionService
                    .ExecuteInTransactionAsync(async () =>
                    {
                        product.Price = 500;

                        await context.SaveChangesAsync();

                        throw new InvalidOperationException(
                            "Simulated failure");
                    });
            });

        // Clear EF Core tracking so we read from database
        context.ChangeTracker.Clear();

        // Assert
        var databaseProduct =
            await context.Products
                .FirstAsync(x => x.Id == product.Id);

        Assert.Equal(originalPrice, databaseProduct.Price);
    }
}