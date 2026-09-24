using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class RemoteSourceDownloadConfiguration : IEntityTypeConfiguration<RemoteSourceDownload>
{
    public void Configure(EntityTypeBuilder<RemoteSourceDownload> builder)
    {
        builder.HasKey(download => download.Id);
        builder.Property(download => download.Id).IsRequired();
        builder.Property(download => download.RemoteSourceAutomationId).IsRequired(false);
        builder.Property(download => download.RemoteSourceRegistrationId).IsRequired(false);
        builder.Property(download => download.SourceName).IsRequired().HasMaxLength(100);
        builder.Property(download => download.RemoteFolderPath).IsRequired().HasColumnType("text");
        builder.Property(download => download.FolderName).IsRequired().HasMaxLength(500);
        builder.Property(download => download.LocalFolderPath).IsRequired().HasColumnType("text");
        builder.Property(download => download.ReleaseTemplateId).IsRequired(false);
        builder
            .Property(download => download.PrimaryLanguageCode)
            .IsRequired(false)
            .HasMaxLength(2);
        builder.Property(download => download.KeepRawFiles).IsRequired();
        builder.Property(download => download.State).IsRequired();
        builder.Property(download => download.FileCount).IsRequired();
        builder.Property(download => download.TotalBytes).IsRequired();
        builder.Property(download => download.LastChangedAt).IsRequired().HasPrecision(4);
        builder.Property(download => download.DiscoveredAt).IsRequired().HasPrecision(4);
        builder.Property(download => download.StartedAt).IsRequired(false).HasPrecision(4);
        builder.Property(download => download.CompletedAt).IsRequired(false).HasPrecision(4);
        builder.Property(download => download.ErrorMessage).IsRequired(false).HasColumnType("text");
        builder.Property(download => download.ReleaseId).IsRequired(false);

        builder
            .HasOne(download => download.RemoteSourceAutomation)
            .WithMany()
            .HasForeignKey(download => download.RemoteSourceAutomationId)
            .HasPrincipalKey(automation => automation.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(download => download.RemoteSourceRegistration)
            .WithMany()
            .HasForeignKey(download => download.RemoteSourceRegistrationId)
            .HasPrincipalKey(registration => registration.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(download => download.ReleaseTemplate)
            .WithMany()
            .HasForeignKey(download => download.ReleaseTemplateId)
            .HasPrincipalKey(template => template.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(download => download.Release)
            .WithMany()
            .HasForeignKey(download => download.ReleaseId)
            .HasPrincipalKey(release => release.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasIndex(download => new
            {
                download.RemoteSourceRegistrationId,
                download.RemoteFolderPath,
            })
            .IsUnique();
        builder.HasIndex(download => new { download.RemoteSourceAutomationId, download.State });
    }
}
