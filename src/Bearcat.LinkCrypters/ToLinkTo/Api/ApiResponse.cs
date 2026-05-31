using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api;

public class ApiResponse<TBody>
{
    [JsonPropertyName("response")]
    public ApiResponseContent<TBody> Response { get; set; } = null!;
}

public class ApiResponseContent<TBody>
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    [JsonPropertyName("errorCode")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("errorMsg")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("body")]
    public TBody? Body { get; set; }
}
