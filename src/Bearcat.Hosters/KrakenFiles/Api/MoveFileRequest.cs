using System.Text.Json.Serialization;

namespace Bearcat.Hosters.KrakenFiles.Api;

public record MoveFileRequest([property: JsonPropertyName("folderId")] string FolderId);
