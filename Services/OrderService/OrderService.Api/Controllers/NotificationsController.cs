using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Notifications;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Api.Controllers
{
    // Stored notifications, so users see what happened while they were offline.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private const int MaxItems = 20;
        private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

        private readonly AppDbContext _db;

        public NotificationsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetMine()
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            var rows = await Visible(userId)
                .Where(n => !_db.NotificationReads.Any(r =>
                    r.NotificationId == n.Id && r.UserId == userId && r.Dismissed))
                .OrderByDescending(n => n.CreatedAt)
                .Take(MaxItems)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Link,
                    n.CreatedAt,
                    Read = _db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == userId)
                })
                .ToListAsync();

            return Ok(rows.Select(n => new NotificationDto(
                n.Id,
                n.Title,
                n.Message,
                n.Link,
                DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
                n.Read)));
        }

        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            if (!await Visible(userId).AnyAsync(n => n.Id == id))
            {
                return NotFound(new { message = "Notification not found." });
            }

            await SetStateAsync(userId, new[] { id }, dismiss: false);

            return NoContent();
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            await SetStateAsync(userId, await Visible(userId).Select(n => n.Id).ToListAsync(), dismiss: false);

            return NoContent();
        }

        [HttpDelete]
        public async Task<IActionResult> Clear()
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            await SetStateAsync(userId, await Visible(userId).Select(n => n.Id).ToListAsync(), dismiss: true);

            return NoContent();
        }

        // Admins also see the broadcast notifications that have no single owner.
        private IQueryable<Notification> Visible(int userId)
        {
            var isAdmin = User.IsInRole("Admin");
            var since = DateTime.UtcNow - Retention;

            return _db.Notifications.Where(n =>
                n.CreatedAt >= since &&
                (n.UserId == userId || (isAdmin && n.UserId == null)));
        }

        private async Task SetStateAsync(int userId, IReadOnlyCollection<int> notificationIds, bool dismiss)
        {
            var existing = await _db.NotificationReads
                .Where(r => r.UserId == userId && notificationIds.Contains(r.NotificationId))
                .ToDictionaryAsync(r => r.NotificationId);

            foreach (var id in notificationIds)
            {
                if (existing.TryGetValue(id, out var state))
                {
                    state.Dismissed |= dismiss;
                }
                else
                {
                    _db.NotificationReads.Add(new NotificationRead
                    {
                        NotificationId = id,
                        UserId = userId,
                        Dismissed = dismiss
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
