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
        builder.Property(r => r.ReleaseFolderPath).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ReleaseInfosCheckedAt).HasPrecision(4).IsRequired(false);

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
            .HasMany(r => r.ReleaseInfos)
            .WithOne(i => i.Release)
            .HasForeignKey(i => i.ReleaseId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
