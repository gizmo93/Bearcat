using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.GetFolderDetails;

public class ResponseBody
{
    [JsonPropertyName("folder")]
    public string Folder { get; set; } = null!;

    [JsonPropertyName("links")]
    public List<FolderLink> Links { get; set; } = [];
}
