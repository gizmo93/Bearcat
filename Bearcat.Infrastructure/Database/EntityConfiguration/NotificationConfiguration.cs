using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(n => n.ResolvedAt).IsRequired(false).HasPrecision(4);
        builder.Property(n => n.NotificationType).IsRequired();
        builder.Property(n => n.Message).IsRequired().HasMaxLength(2000);
    }
}
