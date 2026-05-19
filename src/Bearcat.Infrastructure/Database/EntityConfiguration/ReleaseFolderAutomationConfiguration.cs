using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseFolderAutomationConfiguration
    : IEntityTypeConfiguration<ReleaseFolderAutomation>
{
    public void Configure(EntityTypeBuilder<ReleaseFolderAutomation> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.BasePath).IsRequired().HasMaxLength(1000);
        builder.Property(a => a.FolderNamePattern).IsRequired(false).HasMaxLength(200);
        builder.Property(a => a.ReleaseTemplateId).IsRequired();
        builder.Property(a => a.IsEnabled).IsRequired();

        builder
            .HasOne(a => a.ReleaseTemplate)
            .WithMany()
            .HasForeignKey(a => a.ReleaseTemplateId)
            .HasPrincipalKey(t => t.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
