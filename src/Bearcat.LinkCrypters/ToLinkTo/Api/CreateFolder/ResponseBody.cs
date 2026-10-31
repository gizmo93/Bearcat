using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.CreateFolder;

public class ResponseBody
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;

    [JsonPropertyName("folder")]
    public string Folder { get; set; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;
}
