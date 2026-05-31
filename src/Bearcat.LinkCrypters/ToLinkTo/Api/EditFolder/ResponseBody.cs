using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api.EditFolder;

public class ResponseBody
{
    [JsonPropertyName("affected")]
    public int Affected { get; set; }

    [JsonPropertyName("folder")]
    public string Folder { get; set; } = null!;
}
