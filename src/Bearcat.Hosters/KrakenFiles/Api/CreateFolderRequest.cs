using System.Text.Json.Serialization;

namespace Bearcat.Hosters.KrakenFiles.Api;

public record CreateFolderRequest([property: JsonPropertyName("name")] string Name);
