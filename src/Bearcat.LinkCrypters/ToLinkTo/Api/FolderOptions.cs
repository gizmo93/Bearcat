using System.Text.Json.Serialization;

namespace Bearcat.LinkCrypters.ToLinkTo.Api;

public class FolderOptions
{
    [JsonPropertyName("web")]
    public bool Web { get; set; }

    [JsonPropertyName("container")]
    public bool Container { get; set; }

    [JsonPropertyName("cln")]
    public bool ClickAndLoad { get; set; }

    [JsonPropertyName("captcha")]
    public bool Captcha { get; set; }

    [JsonPropertyName("captcha_text")]
    public bool CaptchaText { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}
