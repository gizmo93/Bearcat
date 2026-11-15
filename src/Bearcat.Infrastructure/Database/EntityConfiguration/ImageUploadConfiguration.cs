using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ImageUploadConfiguration : IEntityTypeConfiguration<ImageUpload>
{
    public void Configure(EntityTypeBuilder<ImageUpload> builder)
    {
        builder.HasKey(upload => upload.Id);
        builder.Property(upload => upload.ImageUploadConfigId).IsRequired();
        builder.Property(upload => upload.ReleaseScreenshotId).IsRequired(false);
        builder.Property(upload => upload.CreatedAt).IsRequired();
        builder.Property(upload => upload.UploadedAt).IsRequired(false);
        builder.Property(upload => upload.UploadState).IsRequired();
        builder.Property(upload => upload.ErrorMessages).HasColumnType("jsonb");

        builder
            .HasOne(upload => upload.ImageUploadConfig)
            .WithMany(config => config.ImageUploads)
            .HasForeignKey(upload => upload.ImageUploadConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(upload => upload.ReleaseScreenshot)
            .WithMany()
            .HasForeignKey(upload => upload.ReleaseScreenshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
