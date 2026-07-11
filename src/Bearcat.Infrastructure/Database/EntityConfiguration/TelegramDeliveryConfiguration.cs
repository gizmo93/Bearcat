using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class TelegramDeliveryConfiguration : IEntityTypeConfiguration<TelegramDelivery>
{
    public void Configure(EntityTypeBuilder<TelegramDelivery> builder)
    {
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(delivery => delivery.DeliveredAt).HasPrecision(4);
        builder.Property(delivery => delivery.NextAttemptAt).HasPrecision(4);
        builder.Property(delivery => delivery.LastError).HasMaxLength(2000);
        builder.HasIndex(delivery => delivery.NotificationId).IsUnique();
        builder.HasIndex(delivery => new { delivery.DeliveredAt, delivery.NextAttemptAt });
        builder
            .HasOne(delivery => delivery.Notification)
            .WithOne()
            .HasForeignKey<TelegramDelivery>(delivery => delivery.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
