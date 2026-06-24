using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseMediaFileConfiguration : IEntityTypeConfiguration<ReleaseMediaFile>
{
    public void Configure(EntityTypeBuilder<ReleaseMediaFile> builder)
    {
        builder.HasKey(file => file.Id);

        builder.Property(file => file.ReleaseId).IsRequired();
        builder.Property(file => file.RelativePath).IsRequired().HasMaxLength(1000);
        builder.Property(file => file.SizeBytes).IsRequired();
        builder.Property(file => file.MediaInfoJson).IsRequired().HasColumnType("jsonb");
        builder.Property(file => file.MediaInfoText).IsRequired();

        builder.HasIndex(file => file.ReleaseId);
    }
}
