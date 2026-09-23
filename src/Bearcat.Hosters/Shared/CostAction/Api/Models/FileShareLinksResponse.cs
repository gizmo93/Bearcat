using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record FileShareLinksResponse(
    [property: JsonPropertyName("for_downloading")] FileShareLinkVariantsResponse? ForDownloading
);
