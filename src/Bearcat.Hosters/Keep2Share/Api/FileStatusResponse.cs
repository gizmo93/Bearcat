using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public class FileStatusResponse
{
    public string Status { get; set; } = null!;

    public int Code { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("is_available")]
    public bool? IsAvailable { get; set; }

    [JsonPropertyName("is_folder")]
    public bool? IsFolder { get; set; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }

    public string? Message { get; set; }
}
