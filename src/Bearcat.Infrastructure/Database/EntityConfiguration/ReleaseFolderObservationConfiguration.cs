using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseFolderObservationConfiguration
    : IEntityTypeConfiguration<ReleaseFolderObservation>
{
    public void Configure(EntityTypeBuilder<ReleaseFolderObservation> builder)
    {
        builder.HasKey(observation => observation.Id);

        builder.Property(observation => observation.FolderPath).IsRequired().HasMaxLength(1000);
        builder.Property(observation => observation.FileCount).IsRequired();
        builder.Property(observation => observation.TotalBytes).IsRequired();
        builder.Property(observation => observation.LastChangedAt).IsRequired();

        builder.HasIndex(observation => observation.FolderPath).IsUnique();
    }
}
