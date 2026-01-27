
using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadConfigurationNotification : IEntityTypeConfiguration<UploadNotification>
{
    public void Configure(EntityTypeBuilder<UploadNotification> builder)
    {
        builder.HasKey(b => b.NotificationId);
        builder.Property(b => b.UploadId);
        
        builder.HasOne(b => b.Upload)
            .WithMany(a => a.Notifications)
            .HasForeignKey(b => b.UploadId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Notification)
            .WithOne(n => n.UploadNotification)
            .HasForeignKey<UploadNotification>(a => a.NotificationId)
            .HasPrincipalKey<Notification>(n => n.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
