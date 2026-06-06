using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ImageHosterRegistrationConfiguration
    : IEntityTypeConfiguration<ImageHosterRegistration>
{
    public void Configure(EntityTypeBuilder<ImageHosterRegistration> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(100).IsRequired();
        builder.Property(h => h.ImageHosterClassName).HasMaxLength(100).IsRequired();
        builder.Property(h => h.SerializedConfig).HasMaxLength(2000).IsRequired();
    }
}
