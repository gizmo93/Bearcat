using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseScreenshotConfiguration : IEntityTypeConfiguration<ReleaseScreenshot>
{
    public void Configure(EntityTypeBuilder<ReleaseScreenshot> builder)
    {
        builder.HasKey(screenshot => screenshot.Id);

        builder.Property(screenshot => screenshot.ReleaseId).IsRequired();
        builder.Property(screenshot => screenshot.Kind).IsRequired();
        builder.Property(screenshot => screenshot.Index).IsRequired();
        builder.Property(screenshot => screenshot.Timestamp).IsRequired(false);
        builder.Property(screenshot => screenshot.RelativeFilePath).IsRequired().HasMaxLength(500);
        builder.Property(screenshot => screenshot.Width).IsRequired();
        builder.Property(screenshot => screenshot.Height).IsRequired();
        builder.Property(screenshot => screenshot.SizeBytes).IsRequired();
        builder.Property(screenshot => screenshot.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(screenshot => screenshot.FileDeletedAt).IsRequired(false).HasPrecision(4);

        builder.HasIndex(screenshot => screenshot.ReleaseId);
    }
}
