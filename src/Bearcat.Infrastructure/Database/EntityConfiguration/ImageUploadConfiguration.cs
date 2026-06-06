using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ImageUploadConfiguration : IEntityTypeConfiguration<ImageUpload>
{
    public void Configure(EntityTypeBuilder<ImageUpload> builder)
    {
        builder.Property(upload => upload.ErrorMessages).HasColumnType("jsonb");
        builder
            .HasOne(upload => upload.ImageUploadConfig)
            .WithMany(config => config.ImageUploads)
            .HasForeignKey(upload => upload.ImageUploadConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
