using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class PostedLocationConfiguration : IEntityTypeConfiguration<PostedLocation>
{
    public void Configure(EntityTypeBuilder<PostedLocation> builder)
    {
        builder.HasKey(location => location.Id);
        builder.Property(location => location.ReleaseId).IsRequired(false);
        builder.Property(location => location.ReleaseCollectionId).IsRequired(false);
        builder.Property(location => location.Url).HasMaxLength(2000).IsRequired();
        builder.Property(location => location.CreatedAt).HasPrecision(4).IsRequired();

        builder
            .HasOne(location => location.Release)
            .WithMany(release => release.PostedLocations)
            .HasForeignKey(location => location.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(location => location.ReleaseCollection)
            .WithMany(collection => collection.PostedLocations)
            .HasForeignKey(location => location.ReleaseCollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_PostedLocation_Owner",
                $"(\"{nameof(PostedLocation.ReleaseId)}\" IS NOT NULL) <> (\"{nameof(PostedLocation.ReleaseCollectionId)}\" IS NOT NULL)"
            )
        );
    }
}
