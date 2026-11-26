using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record PaginationOptions(
    [property: JsonPropertyName("numItems")] int NumItems,
    [property: JsonPropertyName("cursor")] string? Cursor
);
