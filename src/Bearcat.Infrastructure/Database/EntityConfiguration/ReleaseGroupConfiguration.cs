using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseGroupConfiguration : IEntityTypeConfiguration<ReleaseGroup>
{
    public void Configure(EntityTypeBuilder<ReleaseGroup> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(500).IsRequired();
        builder.Property(r => r.EnableAutomaticReuploads).IsRequired();
        builder.Property(r => r.NumberOfHoursUntilReupload).IsRequired();

        builder
            .HasMany(r => r.Releases)
            .WithOne(r => r.ReleaseGroup)
            .HasForeignKey(r => r.ReleaseGroupId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
