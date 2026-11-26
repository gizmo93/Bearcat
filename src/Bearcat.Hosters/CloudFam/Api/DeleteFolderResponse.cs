using System.Text.Json.Serialization;

namespace Bearcat.Hosters.CloudFam.Api;

public record DeleteFolderResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string? Message
);
