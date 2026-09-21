using System.Text.Json.Serialization;

namespace Bearcat.ImageHosters.LoePic.Api;

public class UploadLinks
{
    [JsonPropertyName("direct")]
    public string? Direct { get; init; }

    [JsonPropertyName("page")]
    public string? Page { get; init; }

    [JsonPropertyName("html")]
    public string? Html { get; init; }

    [JsonPropertyName("bbcode")]
    public string? BbCode { get; init; }

    [JsonPropertyName("markdown")]
    public string? Markdown { get; init; }
}
