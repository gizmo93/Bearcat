using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class UploadConfigLinkCryperConfiguration : IEntityTypeConfiguration<UploadConfigLinkCrypter>
{
    public void Configure(EntityTypeBuilder<UploadConfigLinkCrypter> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.UploadConfigId).IsRequired();
        builder.Property(u => u.LinkCrypterRegistrationId).IsRequired();
        builder.Property(u => u.ContainerScope).IsRequired();
        builder.Property(u => u.Password).IsRequired(false).HasMaxLength(100);
        builder.Property(u => u.EnableCaptcha).IsRequired();
        builder.Property(u => u.EnableContainerDownload).IsRequired();
        builder.Property(u => u.EnableClickAndLoad).IsRequired();

        builder
            .HasMany(l => l.LinkCrypterContainers)
            .WithOne(l => l.UploadConfigLinkCrypter)
            .HasForeignKey(l => l.UploadConfigLinkCrypterId)
            .HasPrincipalKey(l => l.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
