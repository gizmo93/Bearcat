using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record ConvexQueryRequest(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("args")] IReadOnlyDictionary<string, string> Arguments,
    [property: JsonPropertyName("format")] string Format
);
