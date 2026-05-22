using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseExternalInfoConfiguration : IEntityTypeConfiguration<ReleaseExternalInfo>
{
    public void Configure(EntityTypeBuilder<ReleaseExternalInfo> builder)
    {
        builder.HasKey(info => info.Id);

        builder.Property(info => info.ReleaseInfoId).IsRequired();
        builder.Property(info => info.Type).IsRequired();
        builder.Property(info => info.Title).IsRequired(false).HasMaxLength(500);
        builder.OwnsMany(
            info => info.Urls,
            urls =>
            {
                urls.ToJson("Urls");
            }
        );
    }
}
