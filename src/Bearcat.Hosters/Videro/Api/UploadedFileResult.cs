using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Videro.Api;

public record UploadedFileResult([property: JsonPropertyName("filecode")] string FileCode);
