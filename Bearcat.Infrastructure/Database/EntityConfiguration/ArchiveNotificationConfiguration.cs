using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ArchiveNotificationConfiguration : IEntityTypeConfiguration<ArchiveNotification>
{
    public void Configure(EntityTypeBuilder<ArchiveNotification> builder)
    {
        builder.HasKey(b => b.NotificationId);
        builder.Property(b => b.ArchiveId);
        
        builder.HasOne(b => b.Archive)
            .WithMany(a => a.Notifications)
            .HasForeignKey(b => b.ArchiveId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Notification)
            .WithOne(n => n.ArchiveNotification)
            .HasForeignKey<ArchiveNotification>(a => a.NotificationId)
            .HasPrincipalKey<Notification>(n => n.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
