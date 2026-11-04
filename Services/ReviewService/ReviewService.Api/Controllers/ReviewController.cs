using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReviewService.Api.Clients;
using ReviewService.Api.Data;
using ReviewService.Api.Dtos;
using ReviewService.Api.Models;

namespace ReviewService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewController(AppDbContext db, IPurchaseVerifier purchases) : ControllerBase
    {
        private const int MaxSummaryProducts = 50;

        private const int MaxPageSize = 50;

        [HttpGet("product/{productId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetForProduct(
            int productId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sort = "newest",
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = db.Reviews.AsNoTracking().Where(x => x.ProductId == productId);

            query = sort switch
            {
                "highest" => query.OrderByDescending(x => x.Rating).ThenByDescending(x => x.CreatedAt),
                "lowest" => query.OrderBy(x => x.Rating).ThenByDescending(x => x.CreatedAt),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };

            var summary = (await SummariesAsync([productId], cancellationToken))[productId];

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var userId = CurrentUserId();

            return Ok(new ReviewPageDto(
                items.Select(x => ToDto(x, userId)).ToList(),
                summary.Count,
                page,
                pageSize,
                summary));
        }

        // Compact star ratings for product cards; ids are comma separated.
        [HttpGet("summary")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSummaries([FromQuery] string productIds, CancellationToken cancellationToken)
        {
            var ids = (productIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .Take(MaxSummaryProducts)
                .ToList();

            var summaries = await SummariesAsync(ids, cancellationToken);

            return Ok(summaries.Values.Where(x => x.Count > 0));
        }

        [HttpGet("product/{productId:int}/me")]
        [Authorize]
        public async Task<IActionResult> GetMine(int productId, CancellationToken cancellationToken)
        {
            if (CurrentUserId() is not { } userId)
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            var existing = await db.Reviews.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductId == productId && x.UserId == userId, cancellationToken);

            if (existing != null)
            {
                return Ok(new MyReviewStatusDto(true, ToDto(existing, userId)));
            }

            try
            {
                return Ok(new MyReviewStatusDto(
                    await purchases.HasPurchasedAsync(userId, productId, cancellationToken),
                    null));
            }
            catch (PurchaseCheckUnavailableException)
            {
                return Ok(new MyReviewStatusDto(false, null));
            }
        }

        [HttpPost("product/{productId:int}")]
        [Authorize]
        public async Task<IActionResult> Create(int productId, [FromBody] ReviewWriteDto dto, CancellationToken cancellationToken)
        {
            if (CurrentUserId() is not { } userId)
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            if (await db.Reviews.AnyAsync(x => x.ProductId == productId && x.UserId == userId, cancellationToken))
            {
                return Conflict(new { message = "You have already reviewed this product. You can edit your review instead." });
            }

            try
            {
                if (!await purchases.HasPurchasedAsync(userId, productId, cancellationToken))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        message = "You can review a product once an order containing it has been delivered."
                    });
                }
            }
            catch (PurchaseCheckUnavailableException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
            }

            var review = new Review
            {
                ProductId = productId,
                UserId = userId,
                ReviewerName = DisplayName(),
                Rating = dto.Rating,
                Title = Clean(dto.Title),
                Body = Clean(dto.Body)
            };

            db.Reviews.Add(review);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Two submissions raced past the check above; the unique index stopped the second.
                return Conflict(new { message = "You have already reviewed this product." });
            }

            return CreatedAtAction(nameof(GetMine), new { productId }, ToDto(review, userId));
        }

        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] ReviewWriteDto dto, CancellationToken cancellationToken)
        {
            if (CurrentUserId() is not { } userId)
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (review == null)
            {
                return NotFound(new { message = "Review not found." });
            }

            if (review.UserId != userId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit your own review." });
            }

            review.Rating = dto.Rating;
            review.Title = Clean(dto.Title);
            review.Body = Clean(dto.Body);
            review.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Ok(ToDto(review, userId));
        }

        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            if (CurrentUserId() is not { } userId)
            {
                return Unauthorized(new { message = "Invalid user identity." });
            }

            var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (review == null)
            {
                return NotFound(new { message = "Review not found." });
            }

            if (review.UserId != userId && !User.IsInRole("Admin"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only delete your own review." });
            }

            db.Reviews.Remove(review);

            await db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private async Task<Dictionary<int, RatingSummaryDto>> SummariesAsync(
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken)
        {
            var rows = await db.Reviews.AsNoTracking()
                .Where(x => productIds.Contains(x.ProductId))
                .GroupBy(x => new { x.ProductId, x.Rating })
                .Select(g => new { g.Key.ProductId, g.Key.Rating, Count = g.Count() })
                .ToListAsync(cancellationToken);

            return productIds.ToDictionary(
                id => id,
                id =>
                {
                    var distribution = new int[5];

                    foreach (var row in rows.Where(x => x.ProductId == id && x.Rating is >= 1 and <= 5))
                    {
                        distribution[row.Rating - 1] = row.Count;
                    }

                    var count = distribution.Sum();
                    var total = distribution.Select((n, index) => n * (index + 1)).Sum();

                    return new RatingSummaryDto(
                        id,
                        count == 0 ? 0 : Math.Round((double)total / count, 1),
                        count,
                        distribution);
                });
        }

        private static ReviewDto ToDto(Review review, int? currentUserId) => new(
            review.Id,
            review.ProductId,
            review.ReviewerName,
            review.Rating,
            review.Title,
            review.Body,
            review.CreatedAt,
            review.UpdatedAt,
            currentUserId == review.UserId);

        private static string? Clean(string? value)
        {
            var trimmed = value?.Trim();

            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        private int? CurrentUserId() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        // "Jane Doe" is shown as "Jane D."
        private string DisplayName()
        {
            var parts = (User.FindFirstValue(ClaimTypes.Name) ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return parts.Length switch
            {
                0 => "Customer",
                1 => parts[0],
                _ => $"{parts[0]} {parts[^1][0]}."
            };
        }
    }
}
