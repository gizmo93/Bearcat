using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.CostAction.Api.Models;

public record UploadUrlRequest([property: JsonPropertyName("apptype")] string AppType);
