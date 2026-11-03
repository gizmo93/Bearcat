using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.ListFolders;

public class ListedFolder
{
    [JsonPropertyName("folder")]
    public string Folder { get; set; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;
}
