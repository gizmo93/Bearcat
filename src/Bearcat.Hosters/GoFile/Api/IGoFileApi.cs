using Bearcat.Hosters.GoFile.Api.CreateFolder;
using Refit;
using Response = Bearcat.Hosters.GoFile.Api.GetAccountId.Response;

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

    [Post("/contents/createFolder")]
    Task<CreateFolder.Response> CreateFolderAsync(
        [Header("Authorization")] string apiToken,
        [Body] Request request,
        CancellationToken cancellationToken = default
    );

    [Get("/contents/{folderId}")]
    Task<GetContent.Response> GetContentAsync(
        string folderId,
        [Header("Authorization")] string apiToken,
        [Query] string? contentFilter = null,
        [Query] string? sortField = null,
        [Query] int? sortDirection = null,
        CancellationToken cancellationToken = default
    );

    [Get("/contents/{fileId}")]
    Task<GetFileInfo.Response> GetFileInfoAsync(
        string fileId,
        [Header("Authorization")] string apiToken,
        [Header("X-Website-Token")] string websiteToken,
        CancellationToken cancellationToken = default
    );
}
