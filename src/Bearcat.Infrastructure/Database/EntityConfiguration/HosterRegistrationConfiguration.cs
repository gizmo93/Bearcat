using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class HosterRegistrationConfiguration : IEntityTypeConfiguration<HosterRegistration>
{
    public void Configure(EntityTypeBuilder<HosterRegistration> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Name).IsRequired().HasMaxLength(100);
        builder.Property(h => h.IsActive).IsRequired();
        builder.Property(h => h.HasUnreadableSecrets).IsRequired();
        builder.Property(h => h.RequiresCaptchaVerification).IsRequired();
        builder.Property(h => h.SerializedConfig).IsRequired().HasMaxLength(4000);
        builder.Property(h => h.HosterClassName).IsRequired().HasMaxLength(500);
        builder.Property(h => h.MaxParallelUploadsOverride);
        builder.Property(h => h.NumberOfHoursUntilReuploadOverride).IsRequired(false);
        builder.Property(h => h.ReuploadTriggerOverride).IsRequired(false);
        builder.Property(h => h.AlwaysReuploadAllFiles).IsRequired();
        builder.Property(h => h.UseForMirrorDownloads).IsRequired();
        builder.Property(h => h.MirrorPriority).IsRequired().HasDefaultValue(100);
        builder.Property(h => h.UploadProxySelection).IsRequired();
        builder.Property(h => h.UploadProxyServerId).IsRequired(false);
        builder.Property(h => h.MirrorDownloadProxySelection).IsRequired();
        builder.Property(h => h.MirrorDownloadProxyServerId).IsRequired(false);

        builder
            .HasOne<ProxyServer>()
            .WithMany()
            .HasForeignKey(h => h.UploadProxyServerId)
            .HasPrincipalKey(proxyServer => proxyServer.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<ProxyServer>()
            .WithMany()
            .HasForeignKey(h => h.MirrorDownloadProxyServerId)
            .HasPrincipalKey(proxyServer => proxyServer.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
