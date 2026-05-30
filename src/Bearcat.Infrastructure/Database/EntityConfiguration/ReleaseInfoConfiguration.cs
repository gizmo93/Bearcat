using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseInfoConfiguration : IEntityTypeConfiguration<ReleaseInfo>
{
    public void Configure(EntityTypeBuilder<ReleaseInfo> builder)
    {
        builder.HasKey(info => info.Id);

        builder.Property(info => info.ReleaseId).IsRequired();
        builder.Property(info => info.NfoDatabaseClassName).IsRequired().HasMaxLength(100);
        builder.Property(info => info.ReleaseName).IsRequired().HasMaxLength(500);
        builder.Property(info => info.ReleaseDatabaseUrl).IsRequired(false).HasMaxLength(1000);
        builder.Property(info => info.SizeNumber).IsRequired(false);
        builder.Property(info => info.SizeUnit).IsRequired(false).HasMaxLength(50);
        builder.Property(info => info.VideoType).IsRequired(false).HasMaxLength(100);
        builder.Property(info => info.AudioType).IsRequired(false).HasMaxLength(100);
        builder.Property(info => info.Genre).IsRequired(false).HasMaxLength(500);
        builder.Property(info => info.Description).IsRequired(false);
        builder.Property(info => info.CoverUrl).IsRequired(false).HasMaxLength(1000);

        builder.HasIndex(info => new { info.ReleaseId, info.NfoDatabaseClassName }).IsUnique();

        builder
            .HasMany(info => info.ExternalInfos)
            .WithOne(externalInfo => externalInfo.ReleaseInfo)
            .HasForeignKey(externalInfo => externalInfo.ReleaseInfoId)
            .HasPrincipalKey(info => info.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(info => info.ReleaseNfo)
            .WithOne(nfo => nfo.ReleaseInfo)
            .HasForeignKey<ReleaseNfo>(nfo => nfo.ReleaseInfoId)
            .HasPrincipalKey<ReleaseInfo>(info => info.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
