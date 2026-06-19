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

    [JsonPropertyName("upload_url")]
    public string? UploadUrlAlias
    {
        get => UploadUrl;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                UploadUrl = value;
            }
        }
    }

    [JsonPropertyName("sess_id")]
    public string? SessionId { get; set; }
}
