using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Streamtape.Api;

public record UploadUrlResult([property: JsonPropertyName("url")] string UploadUrl);
