using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class MediaDatabaseRegistrationConfiguration
    : IEntityTypeConfiguration<MediaDatabaseRegistration>
{
    public void Configure(EntityTypeBuilder<MediaDatabaseRegistration> builder)
    {
        builder.HasKey(registration => registration.Id);

        builder
            .Property(registration => registration.MediaDatabaseClassName)
            .IsRequired()
            .HasMaxLength(100);

        builder
            .Property(registration => registration.SerializedConfig)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(registration => registration.IsActive).IsRequired();

        builder.HasIndex(registration => registration.MediaDatabaseClassName).IsUnique();
    }
}
