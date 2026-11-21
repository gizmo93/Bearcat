using System.Text.Json.Serialization;

namespace Bearcat.DistributionSites.WordPress.Api;

public sealed record EditPostRequest(
    string Content,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Status
);
