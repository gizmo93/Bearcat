using BearCat.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BearCat.Core.Infrastructure.Database.EntityConfiguration;

public class DistributionConfiguration : IEntityTypeConfiguration<Distribution>
{
    public void Configure(EntityTypeBuilder<Distribution> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.ReleaseId).IsRequired();
        builder.Property(d => d.HosterRegistrationId).IsRequired();
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.ArchiverFullClassName).IsRequired().HasMaxLength(200);
        builder.Property(d => d.ArchivePassword).IsRequired(false).HasMaxLength(200);
        builder.Property(d => d.DistributionFolderPath).IsRequired().HasMaxLength(1000);
        builder.Property(d => d.TargetArchiveFileSizeMb).IsRequired();
        builder.Property(d => d.ArchiveNamePrefix).IsRequired(false).HasMaxLength(300);

        builder.HasOne(d => d.Release)
            .WithMany(r => r.Distributions)
            .HasForeignKey(d => d.ReleaseId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasMany(d => d.Archives)
            .WithOne(a => a.Distribution)
            .HasForeignKey(a => a.DistributionId)
            .HasPrincipalKey(d => d.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.ClientCascade);
    }
}
