using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ArchiveConfigTemplateConfiguration : IEntityTypeConfiguration<ArchiveConfigTemplate>
{
    public void Configure(EntityTypeBuilder<ArchiveConfigTemplate> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
        builder.Property(a => a.ArchiveFilesBasePath).IsRequired().HasMaxLength(300);
        builder.Property(a => a.ArchiverName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.ArchivePassword).IsRequired(false).HasMaxLength(100);
        builder.Property(a => a.ArchiveFileSizeMb).IsRequired();
        builder.Property(a => a.UseReleaseNameAsArchiveName).IsRequired();

        builder
            .HasMany(a => a.UploadConfigTemplates)
            .WithOne(u => u.ArchiveConfigTemplate)
            .HasForeignKey(u => u.ArchiveConfigTemplateId)
            .HasPrincipalKey(a => a.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
