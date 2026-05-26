using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseNfoConfiguration : IEntityTypeConfiguration<ReleaseNfo>
{
    public void Configure(EntityTypeBuilder<ReleaseNfo> builder)
    {
        builder.HasKey(nfo => nfo.Id);

        builder.Property(nfo => nfo.ReleaseInfoId).IsRequired();
        builder.Property(nfo => nfo.FileName).IsRequired().HasMaxLength(500);
        builder.Property(nfo => nfo.Content).IsRequired();

        builder.HasIndex(nfo => nfo.ReleaseInfoId).IsUnique();
    }
}
