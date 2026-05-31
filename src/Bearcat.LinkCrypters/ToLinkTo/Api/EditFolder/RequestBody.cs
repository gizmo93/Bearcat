using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.EditFolder;

public class RequestBody
{
    [JsonPropertyName("folder")]
    public string Folder { get; set; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("links")]
    public string Links { get; set; } = null!;

    [JsonPropertyName("options")]
    public FolderOptions Options { get; set; } = null!;
}
