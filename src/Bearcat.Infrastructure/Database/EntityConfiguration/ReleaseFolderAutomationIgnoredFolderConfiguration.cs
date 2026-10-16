using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseFolderAutomationIgnoredFolderConfiguration
    : IEntityTypeConfiguration<ReleaseFolderAutomationIgnoredFolder>
{
    public void Configure(EntityTypeBuilder<ReleaseFolderAutomationIgnoredFolder> builder)
    {
        builder.HasKey(ignoredFolder => ignoredFolder.Id);
        builder.Property(ignoredFolder => ignoredFolder.Id).IsRequired();
        builder.Property(ignoredFolder => ignoredFolder.ReleaseFolderAutomationId).IsRequired();
        builder.Property(ignoredFolder => ignoredFolder.FolderPath).IsRequired().HasMaxLength(1000);
        builder.Property(ignoredFolder => ignoredFolder.IgnoredAt).IsRequired().HasPrecision(4);

        builder
            .HasIndex(ignoredFolder => new
            {
                ignoredFolder.ReleaseFolderAutomationId,
                ignoredFolder.FolderPath,
            })
            .IsUnique();

        builder
            .HasOne(ignoredFolder => ignoredFolder.ReleaseFolderAutomation)
            .WithMany()
            .HasForeignKey(ignoredFolder => ignoredFolder.ReleaseFolderAutomationId)
            .HasPrincipalKey(automation => automation.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
