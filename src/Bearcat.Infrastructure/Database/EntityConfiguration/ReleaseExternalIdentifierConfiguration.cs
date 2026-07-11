using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseExternalIdentifierConfiguration
    : IEntityTypeConfiguration<ReleaseExternalIdentifier>
{
    public void Configure(EntityTypeBuilder<ReleaseExternalIdentifier> builder)
    {
        builder.HasKey(identifier => identifier.Id);
        builder.Property(identifier => identifier.Type).IsRequired();
        builder.Property(identifier => identifier.Value).IsRequired().HasMaxLength(100);
        builder.Property(identifier => identifier.Source).IsRequired();

        builder
            .HasIndex(identifier => new
            {
                identifier.ReleaseId,
                identifier.Type,
                identifier.Value,
                identifier.Source,
            })
            .IsUnique();
    }
}
