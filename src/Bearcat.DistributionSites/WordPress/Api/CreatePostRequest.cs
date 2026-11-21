using System.Text.Json.Serialization;

namespace Bearcat.DistributionSites.WordPress.Api;

public sealed record CreatePostRequest(
    string Title,
    string Status,
    IReadOnlyList<int> Categories,
    string Content,
    [property: JsonPropertyName("featured_media")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? FeaturedMedia
);
