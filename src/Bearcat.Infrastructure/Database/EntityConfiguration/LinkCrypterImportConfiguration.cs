using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class LinkCrypterImportConfiguration : IEntityTypeConfiguration<LinkCrypterImport>
{
    public void Configure(EntityTypeBuilder<LinkCrypterImport> builder)
    {
        builder.HasKey(import => import.Id);
        builder.Property(import => import.Id).IsRequired();
        builder.Property(import => import.LinkCrypterRegistrationId).IsRequired();
        builder.Property(import => import.ReleaseTemplateId).IsRequired();
        builder
            .Property(import => import.ArchiveFilesBasePathForReleasesWithoutLocalFiles)
            .IsRequired(false)
            .HasMaxLength(300);
        builder.Property(import => import.PrimaryLanguageCode).HasMaxLength(2).IsRequired(false);
        builder.Property(import => import.State).IsRequired();
        builder.Property(import => import.CreatedAt).IsRequired().HasPrecision(4);
        builder.Property(import => import.UpdatedAt).IsRequired().HasPrecision(4);
        builder.Property(import => import.ProcessedContainerCount).IsRequired();
        builder.Property(import => import.ContainerCountToProcess).IsRequired();
        builder.Property(import => import.ErrorMessages).IsRequired();

        builder
            .HasOne(import => import.LinkCrypterRegistration)
            .WithMany()
            .HasForeignKey(import => import.LinkCrypterRegistrationId)
            .HasPrincipalKey(registration => registration.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(import => import.ReleaseTemplate)
            .WithMany()
            .HasForeignKey(import => import.ReleaseTemplateId)
            .HasPrincipalKey(template => template.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(import => import.HosterRegistrations)
            .WithOne(hosterRegistration => hosterRegistration.LinkCrypterImport)
            .HasForeignKey(hosterRegistration => hosterRegistration.LinkCrypterImportId)
            .HasPrincipalKey(import => import.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(import => import.Containers)
            .WithOne(container => container.LinkCrypterImport)
            .HasForeignKey(container => container.LinkCrypterImportId)
            .HasPrincipalKey(import => import.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(import => import.State);
        builder.HasIndex(import => import.LinkCrypterRegistrationId);
        builder.HasIndex(import => import.ReleaseTemplateId);
    }
}
