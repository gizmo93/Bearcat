using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class LinkCrypterImportContainerConfiguration
    : IEntityTypeConfiguration<LinkCrypterImportContainer>
{
    public void Configure(EntityTypeBuilder<LinkCrypterImportContainer> builder)
    {
        builder.HasKey(container => container.Id);
        builder.Property(container => container.Id).IsRequired();
        builder.Property(container => container.LinkCrypterImportId).IsRequired();
        builder.Property(container => container.ContainerId).IsRequired().HasMaxLength(200);
        builder.Property(container => container.ContainerUrl).IsRequired().HasMaxLength(200);
        builder.Property(container => container.ContainerName).IsRequired().HasMaxLength(500);
        builder.Property(container => container.ReleaseName).IsRequired().HasMaxLength(500);
        builder
            .Property(container => container.ExternalReference)
            .IsRequired()
            .HasColumnType("text");
        builder.Property(container => container.HasBackupLinks).IsRequired();
        builder.Property(container => container.IsSelected).IsRequired();
        builder.Property(container => container.Status).IsRequired();
        builder.Property(container => container.NotImportableReason).IsRequired(false);
        builder.Property(container => container.ImportedReleaseId).IsRequired(false);
        builder.Property(container => container.ErrorMessages).IsRequired();
        builder.Property(container => container.PreviewJson).IsRequired().HasColumnType("text");

        builder
            .HasOne(container => container.ImportedRelease)
            .WithMany()
            .HasForeignKey(container => container.ImportedReleaseId)
            .HasPrincipalKey(release => release.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(container => new { container.LinkCrypterImportId, container.Status });
        builder.HasIndex(container => container.ImportedReleaseId);
    }
}
