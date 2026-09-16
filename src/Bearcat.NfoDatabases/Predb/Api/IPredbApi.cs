using Refit;

namespace Bearcat.NfoDatabases.Predb.Api;

public interface IPredbApi
{
    [Get("/?type=pre")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<PredbPreResponse>> GetPreAsync(
        [AliasAs("release")] string releaseName,
        CancellationToken cancellationToken = default
    );

    [Get("/?type=nfo")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<PredbNfoResponse>> GetNfoAsync(
        [AliasAs("release")] string releaseName,
        CancellationToken cancellationToken = default
    );
}
