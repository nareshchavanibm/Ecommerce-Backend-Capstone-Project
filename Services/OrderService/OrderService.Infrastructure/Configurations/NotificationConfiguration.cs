using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Title).HasMaxLength(100).IsRequired();

            builder.Property(x => x.Message).HasMaxLength(300).IsRequired();

            builder.Property(x => x.Link).HasMaxLength(200).IsRequired();

            builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        }
    }

    public class NotificationReadConfiguration : IEntityTypeConfiguration<NotificationRead>
    {
        public void Configure(EntityTypeBuilder<NotificationRead> builder)
        {
            builder.ToTable("NotificationReads");

            builder.HasKey(x => new { x.NotificationId, x.UserId });

            builder.HasOne(x => x.Notification)
                .WithMany()
                .HasForeignKey(x => x.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
