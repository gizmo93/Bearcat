using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ConfirmedFolderConfiguration : IEntityTypeConfiguration<ConfirmedFolder>
{
    public void Configure(EntityTypeBuilder<ConfirmedFolder> builder)
    {
        builder.HasKey(confirmedFolder => confirmedFolder.Id);
        builder.Property(confirmedFolder => confirmedFolder.Id).IsRequired();
        builder.Property(confirmedFolder => confirmedFolder.Path).IsRequired().HasMaxLength(1000);
        builder.Property(confirmedFolder => confirmedFolder.MarkerId).IsRequired();
        builder
            .Property(confirmedFolder => confirmedFolder.ConfirmedAt)
            .IsRequired()
            .HasPrecision(4);

        builder.HasIndex(confirmedFolder => confirmedFolder.Path).IsUnique();
    }
}
