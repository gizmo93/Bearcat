using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ImageUploadConfigTemplateConfiguration
    : IEntityTypeConfiguration<ImageUploadConfigTemplate>
{
    public void Configure(EntityTypeBuilder<ImageUploadConfigTemplate> builder)
    {
        builder.HasKey(template => template.Id);
        builder.Property(template => template.ReleaseTemplateId).IsRequired();
        builder.Property(template => template.ImageHosterRegistrationId).IsRequired();
        builder.Property(template => template.Name).IsRequired(false);
        builder.Property(template => template.UploadCover).IsRequired();
        builder.Property(template => template.UploadScreenshots).IsRequired();
        builder.Property(template => template.UploadScreenshotGrid).IsRequired();

        builder
            .HasOne(template => template.ReleaseTemplate)
            .WithMany(releaseTemplate => releaseTemplate.ImageUploadConfigTemplates)
            .HasForeignKey(template => template.ReleaseTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(template => template.ImageHosterRegistration)
            .WithMany(registration => registration.ImageUploadConfigTemplates)
            .HasForeignKey(template => template.ImageHosterRegistrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
