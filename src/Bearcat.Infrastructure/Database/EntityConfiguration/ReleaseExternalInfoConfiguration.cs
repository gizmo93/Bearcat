using System.Text.Json;
using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bearcat.Infrastructure.Database.EntityConfiguration;

public class ReleaseExternalInfoConfiguration : IEntityTypeConfiguration<ReleaseExternalInfo>
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(
        JsonSerializerDefaults.Web
    );
    private static readonly ValueComparer<List<ReleaseExternalInfoUrl>> UrlsComparer = new(
        (left, right) => SerializeUrls(left) == SerializeUrls(right),
        urls => SerializeUrls(urls).GetHashCode(),
        urls => DeserializeUrls(SerializeUrls(urls))
    );

    public void Configure(EntityTypeBuilder<ReleaseExternalInfo> builder)
    {
        builder.HasKey(info => info.Id);

        builder.Property(info => info.ReleaseInfoId).IsRequired();
        builder.Property(info => info.Type).IsRequired();
        builder.Property(info => info.Title).IsRequired(false).HasMaxLength(500);
        builder
            .Property(info => info.Urls)
            .HasConversion(urls => SerializeUrls(urls), json => DeserializeUrls(json))
            .HasColumnType("jsonb");
        builder.Property(info => info.Urls).Metadata.SetValueComparer(UrlsComparer);
    }

    private static string SerializeUrls(List<ReleaseExternalInfoUrl>? urls)
    {
        return JsonSerializer.Serialize(urls ?? [], JsonSerializerOptions);
    }

    private static List<ReleaseExternalInfoUrl> DeserializeUrls(string json)
    {
        return JsonSerializer.Deserialize<List<ReleaseExternalInfoUrl>>(json, JsonSerializerOptions)
            ?? [];
    }
}
