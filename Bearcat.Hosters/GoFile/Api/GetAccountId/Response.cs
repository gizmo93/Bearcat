using System.Text.Json.Serialization;

namespace Bearcat.Hosters.GoFile.Api.GetAccountId;

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

    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;

    [JsonPropertyName("tier")]
    public string Tier { get; set; } = null!;
}
