using ECommerce.Application.DTOs.Products;
using System.Threading.Tasks;

namespace ECommerce.Application.Interfaces;

public interface INotificationService
{
    Task NotifyProductCreated(ProductDto product);

    Task NotifyProductUpdated(ProductDto product);

    Task NotifyProductDeleted(int productId);
}
