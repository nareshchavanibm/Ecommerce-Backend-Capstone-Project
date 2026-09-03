namespace ECommerce.Application.Interfaces;

public interface ITransactionService
{
    Task ExecuteInTransactionAsync(
        Func<Task> operation);
}