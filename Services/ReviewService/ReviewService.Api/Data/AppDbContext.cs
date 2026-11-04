using Microsoft.EntityFrameworkCore;
using ReviewService.Api.Models;

namespace ReviewService.Api.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Review> Reviews => Set<Review>();

        // SQL Server returns unspecified kinds; marking them UTC makes the JSON carry a "Z".
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Review>(review =>
            {
                review.Property(x => x.ReviewerName).HasMaxLength(100).IsRequired();
                review.Property(x => x.Title).HasMaxLength(100);
                review.Property(x => x.Body).HasMaxLength(ReviewLimits.MaxBodyCharacters);

                // One review per customer per product.
                review.HasIndex(x => new { x.ProductId, x.UserId }).IsUnique();
                review.HasIndex(x => new { x.ProductId, x.CreatedAt });
            });
        }
    }
}
