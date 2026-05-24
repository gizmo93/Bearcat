using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fichier.Api.User;

public class UserInfoResponse
{
    public string? Status { get; set; }

    public string? Message { get; set; }

    public string? Email { get; set; }

    public string? Offer { get; set; }

    [JsonPropertyName("upload_forbidden")]
    public string? UploadForbidden { get; set; }
}
