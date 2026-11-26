using System.Text.Json.Serialization;

namespace Bearcat.Hosters.UploadG.Api;

public record FolderEntriesResponse([property: JsonPropertyName("total")] int? Total);
