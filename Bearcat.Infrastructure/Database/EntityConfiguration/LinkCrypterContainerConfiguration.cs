using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class LinkCrypterContainerConfiguration : IEntityTypeConfiguration<LinkCrypterContainer>
{
    public void Configure(EntityTypeBuilder<LinkCrypterContainer> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.UploadConfigLinkCrypterId).IsRequired();
        builder.Property(l => l.UploadId).IsRequired();
        builder.Property(l => l.ExternalReference).IsRequired(false).HasMaxLength(100);
        builder.Property(l => l.ContainerUrl).IsRequired().HasMaxLength(200);
        builder.Property(l => l.State).IsRequired();
        builder.Property(l => l.Password).IsRequired(false).HasMaxLength(100);
        builder.Property(l => l.Errors).IsRequired();
        builder.Property(l => l.CreatedAt).IsRequired().HasPrecision(4);
    }
}
