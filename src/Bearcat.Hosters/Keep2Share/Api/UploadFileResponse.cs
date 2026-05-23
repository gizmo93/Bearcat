using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public class UploadFileResponse
{
    public string Status { get; set; } = null!;

    public bool? Success { get; set; }

    [JsonPropertyName("status_code")]
    public int? StatusCode { get; set; }

    [JsonPropertyName("user_file_id")]
    public string? UserFileId { get; set; }

    public string? Link { get; set; }

    public string? Message { get; set; }
}
