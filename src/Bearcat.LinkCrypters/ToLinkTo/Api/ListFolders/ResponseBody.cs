using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.ListFolders;

public class ResponseBody
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pages")]
    public int Pages { get; set; }

    [JsonPropertyName("folders")]
    public List<ListedFolder> Folders { get; set; } = [];
}
