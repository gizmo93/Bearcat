using Bearcat.Hosters.GoFile.Api.GetAccountId;
using Refit;

namespace Bearcat.Hosters.GoFile.Api;

public interface IGoFileApi
{
    [Get("/accounts/getid")]
    Task<Response> GetAccountAsync(
        [Header("Authorization")] string apiToken,
        CancellationToken cancellationToken = default
    );

    [Get("/accounts/{accountId}")]
    Task<GetAccountInfos.Response> GetAccountInfosAsync(
        string accountId,
        [Header("Authorization")] string apiToken,
        CancellationToken cancellationToken = default
    );

    [Get("/contents/{fileId}?contentFilter=&page=1&pageSize=10&sortField=name&sortDirection=1")]
    Task<GetOnlineStatus.Response> GetOnlineStatusAsync(
        string fileId,
        [Header("Authorization")] string apiToken,
        CancellationToken cancellationToken = default
    );
}
