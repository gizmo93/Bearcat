using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class LinkCrypterRegistrationConfiguration : IEntityTypeConfiguration<LinkCrypterRegistration>
{
    public void Configure(EntityTypeBuilder<LinkCrypterRegistration> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.LinkCrypterClassName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.SerializedConfig)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(l => l.IsActive)
            .IsRequired();
    }
}
