using Refit;

namespace Bearcat.NfoDatabases.Srrdb.Api;

public interface ISrrdbApi
{
    [Get("/v1/details/{releaseName}")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<SrrdbDetailsResponse>> GetDetailsAsync(
        string releaseName,
        CancellationToken cancellationToken = default
    );

    [Get("/v1/imdb/{releaseName}")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<SrrdbImdbResponse>> GetImdbAsync(
        string releaseName,
        CancellationToken cancellationToken = default
    );

    [Get("/v1/nfo/{releaseName}")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<SrrdbNfoResponse>> GetNfoAsync(
        string releaseName,
        CancellationToken cancellationToken = default
    );
}
