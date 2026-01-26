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
        builder.Property(u => u.ContainerName).IsRequired().HasMaxLength(300);
        builder.Property(u => u.Password).IsRequired(false).HasMaxLength(100);

        builder.HasMany(l => l.LinkCrypterContainers)
            .WithOne(l => l.UploadConfigLinkCrypter)
            .HasForeignKey(l => l.UploadConfigLinkCrypterId)
            .HasForeignKey(l => l.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
