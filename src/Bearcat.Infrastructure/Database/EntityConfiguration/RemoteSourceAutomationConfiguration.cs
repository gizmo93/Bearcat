using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class RemoteSourceAutomationConfiguration : IEntityTypeConfiguration<RemoteSourceAutomation>
{
    public void Configure(EntityTypeBuilder<RemoteSourceAutomation> builder)
    {
        builder.HasKey(automation => automation.Id);
        builder.Property(automation => automation.Id).IsRequired();
        builder.Property(automation => automation.Name).IsRequired().HasMaxLength(200);
        builder.Property(automation => automation.RemoteSourceRegistrationId).IsRequired();
        builder.Property(automation => automation.RemotePath).IsRequired().HasColumnType("text");
        builder.Property(automation => automation.TargetPath).IsRequired().HasColumnType("text");
        builder
            .Property(automation => automation.FolderNamePattern)
            .IsRequired(false)
            .HasMaxLength(200);
        builder.Property(automation => automation.ReleaseTemplateId).IsRequired();
        builder
            .Property(automation => automation.PrimaryLanguageCode)
            .IsRequired(false)
            .HasMaxLength(2);
        builder.Property(automation => automation.KeepRawFiles).IsRequired();
        builder.Property(automation => automation.Priority).IsRequired();
        builder.Property(automation => automation.IsEnabled).IsRequired();
        builder.Property(automation => automation.IgnoreExistingOnFirstScan).IsRequired();
        builder.Property(automation => automation.HasCompletedInitialScan).IsRequired();

        builder
            .HasOne(automation => automation.RemoteSourceRegistration)
            .WithMany()
            .HasForeignKey(automation => automation.RemoteSourceRegistrationId)
            .HasPrincipalKey(registration => registration.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(automation => automation.ReleaseTemplate)
            .WithMany()
            .HasForeignKey(automation => automation.ReleaseTemplateId)
            .HasPrincipalKey(template => template.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(automation => new
        {
            automation.RemoteSourceRegistrationId,
            automation.Priority,
        });
    }
}
