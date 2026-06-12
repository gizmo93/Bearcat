using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseCollectionMetadataConfiguration
    : IEntityTypeConfiguration<ReleaseCollectionMetadata>
{
    public void Configure(EntityTypeBuilder<ReleaseCollectionMetadata> builder)
    {
        builder.HasKey(metadata => metadata.Id);

        builder.Property(metadata => metadata.ReleaseCollectionId).IsRequired();
        builder
            .Property(metadata => metadata.SeriesDatabaseClassName)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(metadata => metadata.Title).IsRequired().HasMaxLength(500);
        builder.Property(metadata => metadata.Description).IsRequired(false);
        builder.Property(metadata => metadata.CoverUrl).IsRequired(false).HasMaxLength(1000);
        builder
            .Property(metadata => metadata.SeriesDatabaseUrl)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.HasIndex(metadata => metadata.ReleaseCollectionId).IsUnique();

        builder
            .HasOne(metadata => metadata.ReleaseCollection)
            .WithOne(collection => collection.Metadata)
            .HasForeignKey<ReleaseCollectionMetadata>(metadata => metadata.ReleaseCollectionId)
            .HasPrincipalKey<ReleaseCollection>(collection => collection.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
