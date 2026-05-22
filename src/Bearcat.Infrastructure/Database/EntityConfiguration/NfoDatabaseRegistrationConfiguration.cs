using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class NfoDatabaseRegistrationConfiguration
    : IEntityTypeConfiguration<NfoDatabaseRegistration>
{
    public void Configure(EntityTypeBuilder<NfoDatabaseRegistration> builder)
    {
        builder.HasKey(registration => registration.Id);

        builder
            .Property(registration => registration.NfoDatabaseClassName)
            .IsRequired()
            .HasMaxLength(100);

        builder
            .Property(registration => registration.SerializedConfig)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(registration => registration.IsActive).IsRequired();

        builder.HasIndex(registration => registration.NfoDatabaseClassName).IsUnique();
    }
}
