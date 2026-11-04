using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Configurations
{
    public class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
    {
        public void Configure(EntityTypeBuilder<StockAlert> builder)
        {
            builder.ToTable("StockAlerts");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ProductName).HasMaxLength(200).IsRequired();

            builder.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();

            builder.HasIndex(x => x.ProductId);
        }
    }
}
