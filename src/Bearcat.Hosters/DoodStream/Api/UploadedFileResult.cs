using System.Text.Json.Serialization;

namespace Bearcat.Hosters.DoodStream.Api;

public record UploadedFileResult([property: JsonPropertyName("filecode")] string FileCode);
