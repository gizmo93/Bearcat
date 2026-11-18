using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadCompletionActionRegistrationConfiguration
    : IEntityTypeConfiguration<UploadCompletionActionRegistration>
{
    public void Configure(EntityTypeBuilder<UploadCompletionActionRegistration> builder)
    {
        builder.HasKey(registration => registration.Id);
        builder.Property(registration => registration.Id).IsRequired();
        builder.Property(registration => registration.Name).IsRequired().HasMaxLength(100);

        builder
            .Property(registration => registration.ActionClassName)
            .IsRequired()
            .HasMaxLength(500);

        builder
            .Property(registration => registration.SerializedConfig)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(registration => registration.IsActive).IsRequired();
        builder.Property(registration => registration.RunForInitialUpload).IsRequired();
        builder.Property(registration => registration.RunForReupload).IsRequired();
        builder.Property(registration => registration.HasUnreadableSecrets).IsRequired();
        builder.Property(registration => registration.CreatedAt).IsRequired().HasPrecision(4);
    }
}
