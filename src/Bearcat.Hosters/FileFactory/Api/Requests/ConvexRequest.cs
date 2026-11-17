using System.Text.Json.Serialization;

namespace Bearcat.Hosters.FileFactory.Api.Requests;

public record ConvexRequest<TArguments>(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("args")] TArguments Arguments,
    [property: JsonPropertyName("format")] string Format
);
