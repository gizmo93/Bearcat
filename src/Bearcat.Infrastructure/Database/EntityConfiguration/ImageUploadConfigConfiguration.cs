using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ImageUploadConfigConfiguration : IEntityTypeConfiguration<ImageUploadConfig>
{
    public void Configure(EntityTypeBuilder<ImageUploadConfig> builder)
    {
        builder.HasKey(config => config.Id);
        builder.Property(config => config.ReleaseId).IsRequired(false);
        builder.Property(config => config.ReleaseCollectionId).IsRequired(false);
        builder.Property(config => config.ImageHosterRegistrationId).IsRequired();
        builder.Property(config => config.Name).IsRequired();

        builder
            .HasOne(config => config.Release)
            .WithMany(release => release.ImageUploadConfigs)
            .HasForeignKey(config => config.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(config => config.ReleaseCollection)
            .WithMany(collection => collection.ImageUploadConfigs)
            .HasForeignKey(config => config.ReleaseCollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(config => config.ImageHosterRegistration)
            .WithMany(registration => registration.ImageUploadConfigs)
            .HasForeignKey(config => config.ImageHosterRegistrationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_ImageUploadConfig_Owner",
                "(\"ReleaseId\" IS NOT NULL) <> (\"ReleaseCollectionId\" IS NOT NULL)"
            )
        );
    }
}
