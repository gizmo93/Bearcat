using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.EditFolder;

public class RequestBody
{
    [JsonPropertyName("links")]
    public string Links { get; set; } = null!;

    [JsonPropertyName("options")]
    public FolderOptions Options { get; set; } = null!;
}
