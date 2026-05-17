using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ApplicationConfigurationOverrideConfiguration
    : IEntityTypeConfiguration<ApplicationConfigurationOverride>
{
    public void Configure(EntityTypeBuilder<ApplicationConfigurationOverride> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ConfigurationKey).HasMaxLength(200).IsRequired();
        builder.Property(c => c.PropertyName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.SerializedValue).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired().HasPrecision(4);

        builder.HasIndex(c => new { c.ConfigurationKey, c.PropertyName }).IsUnique();
    }
}
