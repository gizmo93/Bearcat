using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class QualityProfileConfiguration : IEntityTypeConfiguration<QualityProfile>
{
    public void Configure(EntityTypeBuilder<QualityProfile> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(500).IsRequired();

        builder
            .HasMany(p => p.Rules)
            .WithOne(r => r.QualityProfile)
            .HasForeignKey(r => r.QualityProfileId)
            .HasPrincipalKey(p => p.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
