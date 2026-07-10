using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseMetadataConfiguration : IEntityTypeConfiguration<ReleaseMetadata>
{
    public void Configure(EntityTypeBuilder<ReleaseMetadata> builder)
    {
        builder.HasKey(metadata => metadata.Id);
        builder
            .Property(metadata => metadata.MetadataDatabaseClassName)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(metadata => metadata.Title).IsRequired().HasMaxLength(500);
        builder.Property(metadata => metadata.Genre).IsRequired(false).HasMaxLength(500);
        builder.Property(metadata => metadata.Description).IsRequired(false);
        builder.Property(metadata => metadata.CoverUrl).IsRequired(false).HasMaxLength(1000);
        builder
            .Property(metadata => metadata.MetadataDatabaseUrl)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.HasIndex(metadata => metadata.ReleaseId).IsUnique();
    }
}
