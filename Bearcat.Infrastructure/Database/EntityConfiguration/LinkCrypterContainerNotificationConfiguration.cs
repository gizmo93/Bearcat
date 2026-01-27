using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;


public class LinkCrypterContainerNotificationConfiguration : IEntityTypeConfiguration<LinkCrypterContainerNotification>
{
    public void Configure(EntityTypeBuilder<LinkCrypterContainerNotification> builder)
    {
        builder.HasKey(b => b.NotificationId);
        builder.Property(b => b.LinkCrypterContainerId);
        
        builder.HasOne(b => b.LinkCrypterContainer)
            .WithMany(a => a.Notifications)
            .HasForeignKey(b => b.LinkCrypterContainerId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Notification)
            .WithOne(n => n.LinkCrypterContainerNotification)
            .HasForeignKey<LinkCrypterContainerNotification>(a => a.NotificationId)
            .HasPrincipalKey<Notification>(n => n.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
