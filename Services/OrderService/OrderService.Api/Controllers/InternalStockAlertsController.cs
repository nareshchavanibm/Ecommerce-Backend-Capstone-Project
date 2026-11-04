using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Filters;
using OrderService.Api.Notifications;

namespace OrderService.Api.Controllers
{
    // Called by ProductService only; not routed through the API gateway.
    [ApiController]
    [Route("internal/stock-alerts")]
    [InternalApiKey]
    public class InternalStockAlertsController : ControllerBase
    {
        private readonly IOrderNotifier _notifier;

        public InternalStockAlertsController(IOrderNotifier notifier)
        {
            _notifier = notifier;
        }

        public record RestockedRequest(int ProductId);

        [HttpPost("restocked")]
        public async Task<IActionResult> Restocked([FromBody] RestockedRequest request)
        {
            await _notifier.ProductRestockedAsync(request.ProductId);

            return NoContent();
        }
    }
}
