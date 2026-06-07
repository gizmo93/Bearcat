using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseTemplateConfiguration : IEntityTypeConfiguration<ReleaseTemplate>
{
    public void Configure(EntityTypeBuilder<ReleaseTemplate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ReleaseType).IsRequired();
        builder.Property(t => t.ReleaseGroupId).IsRequired();
        builder.Property(t => t.UseReleaseCollections).IsRequired();
        builder.Property(t => t.ReleaseCollectionDetectionMode).IsRequired();
        builder.Property(t => t.ReleaseCollectionPattern).IsRequired(false).HasMaxLength(1000);
        builder
            .Property(t => t.ReleaseCollectionKeyTemplate)
            .IsRequired(false)
            .HasMaxLength(500);
        builder
            .Property(t => t.ReleaseCollectionNameTemplate)
            .IsRequired(false)
            .HasMaxLength(500);

        builder
            .HasOne(t => t.ReleaseGroup)
            .WithMany()
            .HasForeignKey(t => t.ReleaseGroupId)
            .HasPrincipalKey(g => g.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(t => t.ArchiveConfigTemplates)
            .WithOne(a => a.ReleaseTemplate)
            .HasForeignKey(a => a.ReleaseTemplateId)
            .HasPrincipalKey(t => t.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(t => t.UploadConfigTemplates)
            .WithOne(u => u.ReleaseTemplate)
            .HasForeignKey(u => u.ReleaseTemplateId)
            .HasPrincipalKey(t => t.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
