using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class LinkCrypterContainerConfiguration : IEntityTypeConfiguration<LinkCrypterContainer>
{
    public void Configure(EntityTypeBuilder<LinkCrypterContainer> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Scope).IsRequired();
        builder.Property(l => l.UploadConfigLinkCrypterId).IsRequired(false);
        builder.Property(l => l.UploadId).IsRequired(false);
        builder.Property(l => l.CollectionUploadSlotId).IsRequired(false);
        builder.Property(l => l.LinkCrypterRegistrationId).IsRequired();
        builder.Property(l => l.ExternalReference).IsRequired(false).HasMaxLength(100);
        builder.Property(l => l.ContainerUrl).IsRequired().HasMaxLength(200);
        builder.Property(l => l.State).IsRequired();
        builder.Property(l => l.Password).IsRequired(false).HasMaxLength(100);
        builder.Property(l => l.EnableCaptcha).IsRequired();
        builder.Property(l => l.EnableContainerDownload).IsRequired();
        builder.Property(l => l.EnableClickAndLoad).IsRequired();
        builder.Property(l => l.Errors).IsRequired();
        builder.Property(l => l.CreatedAt).IsRequired().HasPrecision(4);

        builder
            .HasOne(l => l.LinkCrypterRegistration)
            .WithMany()
            .HasForeignKey(l => l.LinkCrypterRegistrationId)
            .HasPrincipalKey(r => r.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(l => l.CollectionUploadSlot)
            .WithMany()
            .HasForeignKey(l => l.CollectionUploadSlotId)
            .HasPrincipalKey(s => s.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
