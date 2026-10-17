using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Videro.Api;

public record FileInfoResult([property: JsonPropertyName("file_code")] string FileCode);
