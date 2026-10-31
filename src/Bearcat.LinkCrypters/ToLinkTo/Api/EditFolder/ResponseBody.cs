using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.EditFolder;

public class ResponseBody
{
    [JsonPropertyName("folder")]
    public string Folder { get; set; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("affected")]
    public int Affected { get; set; }
}
