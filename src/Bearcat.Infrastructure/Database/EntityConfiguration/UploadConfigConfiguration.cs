using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadConfigConfiguration : IEntityTypeConfiguration<UploadConfig>
{
    public void Configure(EntityTypeBuilder<UploadConfig> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.HosterRegistrationId).IsRequired(false);
        builder.Property(u => u.HosterAccountPoolId).IsRequired(false);
        builder.Property(u => u.UploadSource).IsRequired();
        builder.Property(u => u.ArchiveConfigId).IsRequired(false);
        builder.Property(u => u.CollectionUploadSlotId).IsRequired(false);
        builder.Property(u => u.Name).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PremiumOnlyDownload).IsRequired();

        builder
            .HasOne(u => u.HosterRegistration)
            .WithMany(h => h.UploadConfigs)
            .HasForeignKey(u => u.HosterRegistrationId)
            .HasPrincipalKey(h => h.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(u => u.HosterAccountPool)
            .WithMany(pool => pool.UploadConfigs)
            .HasForeignKey(u => u.HosterAccountPoolId)
            .HasPrincipalKey(pool => pool.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(u => u.Uploads)
            .WithOne(u => u.UploadConfig)
            .HasForeignKey(u => u.UploadConfigId)
            .HasPrincipalKey(u => u.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(u => u.LinkCrypters)
            .WithOne(l => l.UploadConfig)
            .HasForeignKey(l => l.UploadConfigId)
            .HasPrincipalKey(u => u.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(u => u.CollectionUploadSlot)
            .WithMany(s => s.UploadConfigs)
            .HasForeignKey(u => u.CollectionUploadSlotId)
            .HasPrincipalKey(s => s.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_UploadConfig_UploadTarget",
                "(\"HosterRegistrationId\" IS NULL) <> (\"HosterAccountPoolId\" IS NULL)"
            );
            table.HasCheckConstraint(
                "CK_UploadConfig_UploadSource",
                "(\"UploadSource\" = 1) = (\"ArchiveConfigId\" IS NOT NULL)"
            );
        });
    }
}
