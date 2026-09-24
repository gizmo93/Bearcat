using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class RemoteSourceRegistrationConfiguration
    : IEntityTypeConfiguration<RemoteSourceRegistration>
{
    public void Configure(EntityTypeBuilder<RemoteSourceRegistration> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).IsRequired();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.SerializedConfig).IsRequired().HasColumnType("text");
        builder.Property(r => r.SourceClassName).IsRequired().HasMaxLength(500);
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.MaxConnections).IsRequired();
    }
}
