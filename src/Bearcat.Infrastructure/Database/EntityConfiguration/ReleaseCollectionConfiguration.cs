using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseCollectionConfiguration : IEntityTypeConfiguration<ReleaseCollection>
{
    public void Configure(EntityTypeBuilder<ReleaseCollection> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ReleaseGroupId).IsRequired();
        builder.Property(c => c.Key).IsRequired().HasMaxLength(500);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(500);
        builder.Property(c => c.CreatedAt).IsRequired().HasPrecision(4);

        builder.HasIndex(c => new { c.ReleaseGroupId, c.Key }).IsUnique();

        builder
            .HasOne(c => c.ReleaseGroup)
            .WithMany()
            .HasForeignKey(c => c.ReleaseGroupId)
            .HasPrincipalKey(g => g.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(c => c.Releases)
            .WithOne(r => r.ReleaseCollection)
            .HasForeignKey(r => r.ReleaseCollectionId)
            .HasPrincipalKey(c => c.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
