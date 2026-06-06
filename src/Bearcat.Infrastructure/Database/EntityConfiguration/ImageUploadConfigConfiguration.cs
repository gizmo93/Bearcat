using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ImageUploadConfigConfiguration : IEntityTypeConfiguration<ImageUploadConfig>
{
    public void Configure(EntityTypeBuilder<ImageUploadConfig> builder)
    {
        builder.Property(config => config.Name).IsRequired();
        builder
            .HasOne(config => config.Release)
            .WithMany(release => release.ImageUploadConfigs)
            .HasForeignKey(config => config.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(config => config.ImageHosterRegistration)
            .WithMany(registration => registration.ImageUploadConfigs)
            .HasForeignKey(config => config.ImageHosterRegistrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
