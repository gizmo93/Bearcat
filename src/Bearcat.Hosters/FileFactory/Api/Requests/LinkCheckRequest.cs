using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record LinkCheckRequest([property: JsonPropertyName("links")] string Links);
