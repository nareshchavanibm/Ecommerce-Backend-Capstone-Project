using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Api.Controllers
{
    // "Notify me" requests for out-of-stock products.
    [ApiController]
    [Route("api/notifications/stock-alerts")]
    [Authorize]
    public class StockAlertsController : ControllerBase
    {
        private const int MaxNameLength = 200;

        private readonly AppDbContext _db;

        public StockAlertsController(AppDbContext db)
        {
            _db = db;
        }

        public record SubscribeRequest(string? ProductName);

        [HttpGet]
        public async Task<IActionResult> GetMine()
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            var ids = await _db.StockAlerts
                .Where(a => a.UserId == userId)
                .Select(a => a.ProductId)
                .ToListAsync();

            return Ok(ids);
        }

        [HttpPut("{productId:int}")]
        public async Task<IActionResult> Subscribe(int productId, [FromBody] SubscribeRequest request)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            if (productId <= 0)
            {
                return BadRequest(new { message = "Product ID must be greater than 0." });
            }

            if (await _db.StockAlerts.AnyAsync(a => a.UserId == userId && a.ProductId == productId))
            {
                return NoContent();
            }

            var name = request.ProductName?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                name = "A product you asked about";
            }
            else if (name.Length > MaxNameLength)
            {
                name = name[..MaxNameLength];
            }

            _db.StockAlerts.Add(new StockAlert { UserId = userId, ProductId = productId, ProductName = name });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent identical request already saved it.
            }

            return NoContent();
        }

        [HttpDelete("{productId:int}")]
        public async Task<IActionResult> Unsubscribe(int productId)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            await _db.StockAlerts
                .Where(a => a.UserId == userId && a.ProductId == productId)
                .ExecuteDeleteAsync();

            return NoContent();
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
