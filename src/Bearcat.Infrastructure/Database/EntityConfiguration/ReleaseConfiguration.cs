using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(500).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(r => r.ReleaseType).IsRequired();
        builder.Property(r => r.ReleaseContentType).IsRequired();
        builder.Property(r => r.PrimaryLanguageCode).HasMaxLength(2).IsRequired(false);
        builder.Property(r => r.ReleaseFolderPath).HasMaxLength(1000).IsRequired(false);
        builder.Property(r => r.ReleaseInfoCheckedAt).HasPrecision(4).IsRequired(false);
        builder.Property(r => r.MediaMetadataExtractedAt).HasPrecision(4).IsRequired(false);
        builder.Property(r => r.UploadsPostedAt).HasPrecision(4).IsRequired(false);
        builder.Property(r => r.ReleaseCollectionId).IsRequired(false);
        builder.Property(r => r.QualityGateState).IsRequired();
        builder.Property(r => r.QualityGateEvaluatedAt).HasPrecision(4).IsRequired(false);

        builder.HasIndex(r => r.ReleaseFolderPath);
        builder.HasIndex(r => r.QualityGateState);

        builder
            .HasMany(r => r.ArchiveConfigs)
            .WithOne(a => a.Release)
            .HasForeignKey(a => a.ReleaseId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(r => r.UploadConfigs)
            .WithOne(u => u.Release)
            .HasForeignKey(u => u.ReleaseId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(r => r.ReleaseInfo)
            .WithOne(i => i.Release)
            .HasForeignKey<ReleaseInfo>(i => i.ReleaseId)
            .HasPrincipalKey<Release>(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(r => r.ReleaseNfo)
            .WithOne(nfo => nfo.Release)
            .HasForeignKey<ReleaseNfo>(nfo => nfo.ReleaseId)
            .HasPrincipalKey<Release>(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(r => r.ExternalIdentifiers)
            .WithOne(identifier => identifier.Release)
            .HasForeignKey(identifier => identifier.ReleaseId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(r => r.MediaFiles)
            .WithOne(file => file.Release)
            .HasForeignKey(file => file.ReleaseId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(r => r.QualityIssues)
            .WithOne(issue => issue.Release)
            .HasForeignKey(issue => issue.ReleaseId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
