using Refit;

namespace Bearcat.NfoDatabases.Xrel.Api;

public interface IXrelApi
{
    [Get("/v2/release/info.json")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<XrelRelease>> GetReleaseInfoAsync(
        [AliasAs("dirname")] string dirname,
        CancellationToken cancellationToken = default
    );

    [Get("/v2/p2p/rls_info.json")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<XrelP2pRelease>> GetP2pReleaseInfoAsync(
        [AliasAs("dirname")] string dirname,
        CancellationToken cancellationToken = default
    );
}
