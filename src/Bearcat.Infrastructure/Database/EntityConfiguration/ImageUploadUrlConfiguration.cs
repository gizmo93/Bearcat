using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ImageUploadUrlConfiguration : IEntityTypeConfiguration<ImageUploadUrl>
{
    public void Configure(EntityTypeBuilder<ImageUploadUrl> builder)
    {
        builder.Property(url => url.Url).IsRequired();
        builder
            .HasOne(url => url.ImageUpload)
            .WithMany(upload => upload.ImageUrls)
            .HasForeignKey(url => url.ImageUploadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
