using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Vidara.Api;

public record UploadedFileResult([property: JsonPropertyName("filecode")] string EmbedUrl);
