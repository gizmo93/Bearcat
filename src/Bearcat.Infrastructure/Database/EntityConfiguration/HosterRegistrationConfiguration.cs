using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class HosterRegistrationConfiguration : IEntityTypeConfiguration<HosterRegistration>
{
    public void Configure(EntityTypeBuilder<HosterRegistration> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Name).IsRequired().HasMaxLength(100);
        builder.Property(h => h.IsActive).IsRequired();
        builder.Property(h => h.SerializedConfig).IsRequired().HasMaxLength(4000);
        builder.Property(h => h.HosterClassName).IsRequired().HasMaxLength(500);
    }
}
