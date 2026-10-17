using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Vidara.Api;

public record UploadServerResult([property: JsonPropertyName("upload_server")] string UploadUrl);
