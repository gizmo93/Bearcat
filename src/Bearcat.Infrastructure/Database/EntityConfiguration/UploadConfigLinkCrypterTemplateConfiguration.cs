using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadConfigLinkCrypterTemplateConfiguration
    : IEntityTypeConfiguration<UploadConfigLinkCrypterTemplate>
{
    public void Configure(EntityTypeBuilder<UploadConfigLinkCrypterTemplate> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.LinkCrypterRegistrationId).IsRequired();
        builder.Property(l => l.Password).IsRequired(false).HasMaxLength(100);
        builder.Property(l => l.EnableCaptcha).IsRequired();
        builder.Property(l => l.EnableContainerDownload).IsRequired();
        builder.Property(l => l.EnableClickAndLoad).IsRequired();

        builder
            .HasOne(l => l.LinkCrypterRegistration)
            .WithMany()
            .HasForeignKey(l => l.LinkCrypterRegistrationId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
