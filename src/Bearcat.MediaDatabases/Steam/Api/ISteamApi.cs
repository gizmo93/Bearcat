using Refit;

namespace Bearcat.MediaDatabases.Steam.Api;

public interface ISteamApi
{
    [Get("/api/appdetails")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<Dictionary<string, SteamAppDetailsResponse>>> GetAppDetailsAsync(
        [AliasAs("appids")] string appIds,
        [AliasAs("l")] string language,
        CancellationToken cancellationToken = default
    );

    [Get("/api/storesearch/")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<SteamStoreSearchResponse>> SearchStoreAsync(
        [AliasAs("term")] string term,
        [AliasAs("l")] string language,
        [AliasAs("cc")] string countryCode,
        CancellationToken cancellationToken = default
    );
}
