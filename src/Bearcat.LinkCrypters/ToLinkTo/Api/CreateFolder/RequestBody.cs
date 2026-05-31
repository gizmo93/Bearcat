using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.CreateFolder;

public class RequestBody
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("links")]
    public string Links { get; set; } = null!;

    [JsonPropertyName("options")]
    public FolderOptions Options { get; set; } = null!;
}
