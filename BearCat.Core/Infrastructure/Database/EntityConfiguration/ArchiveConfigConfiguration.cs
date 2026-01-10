using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BearCat.Core.Infrastructure.Database.EntityConfiguration;

public class ArchiveConfigConfiguration : IEntityTypeConfiguration<ArchiveConfig>
{
    public void Configure(EntityTypeBuilder<ArchiveConfig> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ReleaseId).IsRequired();
        builder.Property(a => a.ArchiverFullClassName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.ArchivePassword).HasMaxLength(100).IsRequired(false);
        builder.Property(a => a.ArchiveFileSizeMb).IsRequired();
        builder.Property(a => a.ArchiveNamePrefix).IsRequired().HasMaxLength(200);
        builder.Property(a => a.ArchiveFilesBasePath).IsRequired().HasMaxLength(300);

        builder.HasMany(a => a.Archives)
            .WithOne(a => a.ArchiveConfig)
            .HasForeignKey(a => a.ArchiveConfigId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.UploadConfigs)
            .WithOne(u => u.ArchiveConfig)
            .HasForeignKey(u => u.ArchiveConfigId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
