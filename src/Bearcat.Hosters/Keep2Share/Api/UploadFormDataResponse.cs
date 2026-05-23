using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public class UploadFormDataResponse
{
    public string Status { get; set; } = null!;

    public int Code { get; set; }

    [JsonPropertyName("form_action")]
    public string? FormAction { get; set; }

    [JsonPropertyName("file_field")]
    public string? FileField { get; set; }

    [JsonPropertyName("form_data")]
    public Dictionary<string, JsonElement> FormData { get; set; } = [];

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }

    public string? Message { get; set; }
}
