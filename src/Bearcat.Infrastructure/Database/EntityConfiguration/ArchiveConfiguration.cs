using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ArchiveConfiguration : IEntityTypeConfiguration<Archive>
{
    public void Configure(EntityTypeBuilder<Archive> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ArchiveConfigId);
        builder.Property(a => a.ArchiveFolderPath).HasMaxLength(500).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(a => a.ErrorMessages);
        builder.Property(a => a.ArchiveState).IsRequired();
        builder.Property(a => a.ArchiveFileSizeMb).IsRequired();

        builder
            .HasMany(a => a.ArchiveFiles)
            .WithOne(a => a.Archive)
            .HasForeignKey(a => a.ArchiveId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(a => a.Uploads)
            .WithOne(u => u.Archive)
            .HasForeignKey(u => u.ArchiveId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
