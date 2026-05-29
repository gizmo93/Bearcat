using System.Text.Json.Serialization;

namespace Bearcat.Hosters.GoFile.Api.GetFileInfo;

public class Response
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    [JsonPropertyName("data")]
    public Data? Data { get; set; }
}

public class Data
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;
}
