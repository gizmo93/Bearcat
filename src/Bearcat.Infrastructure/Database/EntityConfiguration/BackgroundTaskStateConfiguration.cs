using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class BackgroundTaskStateConfiguration : IEntityTypeConfiguration<BackgroundTaskState>
{
    public void Configure(EntityTypeBuilder<BackgroundTaskState> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Key).IsRequired().HasMaxLength(500);
        builder.Property(t => t.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.IsEnabled).IsRequired();
        builder.Property(t => t.LastStartedAt).HasPrecision(4);
        builder.Property(t => t.LastFinishedAt).HasPrecision(4);
        builder.Property(t => t.LastErrorMessage).HasMaxLength(2000);
        builder.Property(t => t.UpdatedAt).IsRequired().HasPrecision(4);

        builder.HasIndex(t => t.Key).IsUnique();
    }
}
