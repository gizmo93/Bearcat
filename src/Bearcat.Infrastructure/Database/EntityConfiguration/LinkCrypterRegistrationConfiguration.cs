using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class LinkCrypterRegistrationConfiguration
    : IEntityTypeConfiguration<LinkCrypterRegistration>
{
    public void Configure(EntityTypeBuilder<LinkCrypterRegistration> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);

        builder.Property(l => l.LinkCrypterClassName).IsRequired().HasMaxLength(50);

        builder.Property(l => l.SerializedConfig).IsRequired().HasMaxLength(4000);

        builder.Property(l => l.IsActive).IsRequired();

        builder.Property(l => l.HasUnreadableSecrets).IsRequired();
        builder.Property(l => l.ProxySelection).IsRequired();
        builder.Property(l => l.ProxyServerId).IsRequired(false);

        builder
            .HasOne<ProxyServer>()
            .WithMany()
            .HasForeignKey(l => l.ProxyServerId)
            .HasPrincipalKey(proxyServer => proxyServer.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
