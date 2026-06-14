using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class CollectionImageUploadConfigTemplateConfiguration
    : IEntityTypeConfiguration<CollectionImageUploadConfigTemplate>
{
    public void Configure(EntityTypeBuilder<CollectionImageUploadConfigTemplate> builder)
    {
        builder.HasKey(template => template.Id);
        builder.Property(template => template.ReleaseTemplateId).IsRequired();
        builder.Property(template => template.ImageHosterRegistrationId).IsRequired();
        builder.Property(template => template.Name).IsRequired(false);

        builder
            .HasOne(template => template.ReleaseTemplate)
            .WithMany(releaseTemplate => releaseTemplate.CollectionImageUploadConfigTemplates)
            .HasForeignKey(template => template.ReleaseTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(template => template.ImageHosterRegistration)
            .WithMany(registration => registration.CollectionImageUploadConfigTemplates)
            .HasForeignKey(template => template.ImageHosterRegistrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
