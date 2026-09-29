using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ProxyCategoryDefaultConfiguration : IEntityTypeConfiguration<ProxyCategoryDefault>
{
    public void Configure(EntityTypeBuilder<ProxyCategoryDefault> builder)
    {
        builder.HasKey(proxyCategoryDefault => proxyCategoryDefault.ProxyCategory);
        builder
            .Property(proxyCategoryDefault => proxyCategoryDefault.ProxyCategory)
            .IsRequired()
            .ValueGeneratedNever();
        builder
            .Property(proxyCategoryDefault => proxyCategoryDefault.ProxyServerId)
            .IsRequired(false);

        builder
            .HasOne(proxyCategoryDefault => proxyCategoryDefault.ProxyServer)
            .WithMany()
            .HasForeignKey(proxyCategoryDefault => proxyCategoryDefault.ProxyServerId)
            .HasPrincipalKey(proxyServer => proxyServer.Id)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
