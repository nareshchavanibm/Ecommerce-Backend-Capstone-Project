using ECommerce.Application.Interfaces;

namespace ECommerce.Infrastructure.Data;

public class TransactionService(
    AppDbContext context) : ITransactionService
{
    public async Task ExecuteInTransactionAsync(
        Func<Task> operation)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync();

        try
        {
            await operation();

            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();

            throw;
        }
    }
}