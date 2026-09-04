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
        builder.Property(n => n.NotificationSeverity).IsRequired();
        builder.Property(n => n.NotificationKind).IsRequired();
        builder.Property(n => n.Message).IsRequired().HasMaxLength(2000);

        builder
            .HasIndex(n => new { n.CreatedAt, n.Id })
            .HasFilter($"\"{nameof(Notification.ResolvedAt)}\" IS NULL")
            .IsDescending();

        builder
            .HasOne(n => n.LinkCrypterContainer)
            .WithMany(l => l.Notifications)
            .HasForeignKey(n => n.LinkCrypterContainerId)
            .HasPrincipalKey(l => l.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(n => n.Upload)
            .WithMany(l => l.Notifications)
            .HasForeignKey(n => n.UploadId)
            .HasPrincipalKey(l => l.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(n => n.Archive)
            .WithMany(l => l.Notifications)
            .HasForeignKey(n => n.ArchiveId)
            .HasPrincipalKey(l => l.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(n => n.Release)
            .WithMany(l => l.Notifications)
            .HasForeignKey(n => n.ReleaseId)
            .HasPrincipalKey(l => l.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
