using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public class RequestUploadResponse
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = null!;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("result")]
    public string? UploadUrl { get; set; }

    [JsonPropertyName("sess_id")]
    public string? SessionId { get; set; }
}
