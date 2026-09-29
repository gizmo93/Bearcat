using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ProxyServerConfiguration : IEntityTypeConfiguration<ProxyServer>
{
    public void Configure(EntityTypeBuilder<ProxyServer> builder)
    {
        builder.HasKey(proxyServer => proxyServer.Id);
        builder.Property(proxyServer => proxyServer.Id).IsRequired();
        builder.Property(proxyServer => proxyServer.Name).IsRequired().HasMaxLength(100);
        builder.Property(proxyServer => proxyServer.ProxyType).IsRequired();
        builder.Property(proxyServer => proxyServer.Host).IsRequired().HasMaxLength(255);
        builder.Property(proxyServer => proxyServer.Port).IsRequired();
        builder.Property(proxyServer => proxyServer.Username).IsRequired(false).HasMaxLength(255);
        builder
            .Property(proxyServer => proxyServer.EncryptedPassword)
            .IsRequired(false)
            .HasColumnType("text");
        builder.Property(proxyServer => proxyServer.HasUnreadableSecrets).IsRequired();

        builder.HasIndex(proxyServer => proxyServer.Name).IsUnique();
    }
}
