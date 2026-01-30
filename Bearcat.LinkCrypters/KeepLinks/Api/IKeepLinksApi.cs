using Refit;

namespace Bearcat.LinkCrypters.KeepLinks.Api;

public interface IKeepLinksApi
{
    [Get("/api.php?list=1&page=1&pagesize=1&output=json")]
    Task<string> GetLinksAsync(
        [Query][AliasAs("apihash")] string apiKey,
        CancellationToken cancellationToken = default);
    
    [Post("/api.php?captcha=on&captchatype=Re&output=json")]
    Task<ProtectLinks.Response> ProtectLinkAsync(
        [Query][AliasAs("apihash")] string apiKey,
        [Query][AliasAs("link-to-protect")] string linksToProtect,
        [Query][AliasAs("password")] string? password,
        [Query][AliasAs("title")] string? title,
        CancellationToken cancellationToken = default);
    
    [Post("/api.php?captcha=on&captchatype=Re&output=json")]
    Task<ProtectLinks.Response> UpdateContainerAsync(
        [Query][AliasAs("apihash")] string apiKey,
        [Query][AliasAs("link-to-protect")] string linksToProtect,
        [Query][AliasAs("url-id")] string urlId,
        CancellationToken cancellationToken = default);
}
