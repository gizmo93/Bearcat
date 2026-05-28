using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Keep2Share.Api;

public record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("re_captcha_challenge")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? ReCaptchaChallenge = null,
    [property: JsonPropertyName("re_captcha_response")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? ReCaptchaResponse = null
);

public record AuthenticatedRequest([property: JsonPropertyName("auth_token")] string AuthToken);

public record UploadFormDataRequest(
    [property: JsonPropertyName("auth_token")] string AuthToken,
    [property: JsonPropertyName("parent_id")] string? ParentId = null,
    [property: JsonPropertyName("preferred_node")] string? PreferredNode = null
);

public record FileStatusRequest([property: JsonPropertyName("id")] string Id);

public record GetFilesInfoRequest(
    [property: JsonPropertyName("auth_token")] string AuthToken,
    [property: JsonPropertyName("ids")] IReadOnlyList<string> Ids
);
