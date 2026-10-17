using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Playmate.Api;

public record UploadedFileResult([property: JsonPropertyName("filecode")] string EmbedUrl);
