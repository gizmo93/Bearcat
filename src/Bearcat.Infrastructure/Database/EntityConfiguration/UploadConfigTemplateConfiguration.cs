using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadConfigTemplateConfiguration : IEntityTypeConfiguration<UploadConfigTemplate>
{
    public void Configure(EntityTypeBuilder<UploadConfigTemplate> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.HosterRegistrationId).IsRequired(false);
        builder.Property(u => u.HosterAccountPoolId).IsRequired(false);
        builder.Property(u => u.UploadSource).IsRequired();
        builder.Property(u => u.ArchiveConfigTemplateId).IsRequired(false);
        builder.Property(u => u.Name).IsRequired(false).HasMaxLength(200);
        builder.Property(u => u.PremiumOnlyDownload).IsRequired();
        builder.Property(u => u.CollectionUploadSlotKey).IsRequired(false).HasMaxLength(200);
        builder.Property(u => u.CollectionUploadSlotName).IsRequired(false).HasMaxLength(200);
        builder.Property(u => u.CollectionUploadSlotIsRequired).IsRequired();
        builder.Property(u => u.CollectionUploadSlotPasswordPolicy).IsRequired();
        builder
            .Property(u => u.CollectionUploadSlotExpectedArchivePassword)
            .IsRequired(false)
            .HasMaxLength(100);

        builder
            .HasOne(u => u.HosterRegistration)
            .WithMany()
            .HasForeignKey(u => u.HosterRegistrationId)
            .HasPrincipalKey(h => h.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(u => u.HosterAccountPool)
            .WithMany(pool => pool.UploadConfigTemplates)
            .HasForeignKey(u => u.HosterAccountPoolId)
            .HasPrincipalKey(pool => pool.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(u => u.LinkCrypterTemplates)
            .WithOne(l => l.UploadConfigTemplate)
            .HasForeignKey(l => l.UploadConfigTemplateId)
            .HasPrincipalKey(u => u.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_UploadConfigTemplate_UploadTarget",
                "(\"HosterRegistrationId\" IS NULL) <> (\"HosterAccountPoolId\" IS NULL)"
            );
            table.HasCheckConstraint(
                "CK_UploadConfigTemplate_UploadSource",
                "(\"UploadSource\" = 1) = (\"ArchiveConfigTemplateId\" IS NOT NULL)"
            );
        });
    }
}
