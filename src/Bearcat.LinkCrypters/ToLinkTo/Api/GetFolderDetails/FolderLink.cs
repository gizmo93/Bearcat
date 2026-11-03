using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.GetFolderDetails;

public class FolderLink
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;
}
