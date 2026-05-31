using Bearcat.LinkCrypters.ToLinkTo.Api.CreateFolder;
using Refit;

namespace Bearcat.LinkCrypters.ToLinkTo.Api;

public interface IToLinkToApi
{
    [Post("/api/v1/ping")]
    Task<ApiResponse<string>> PingAsync(
        [Body] ApiRequest<Ping.RequestBody> request,
        CancellationToken cancellationToken
    );

    [Post("/api/v1/folder/create")]
    Task<ApiResponse<string>> CreateFolderAsync(
        [Body] ApiRequest<RequestBody> request,
        CancellationToken cancellationToken
    );

    [Post("/api/v1/folder/edit")]
    Task<ApiResponse<EditFolder.ResponseBody>> EditFolderAsync(
        [Body] ApiRequest<EditFolder.RequestBody> request,
        CancellationToken cancellationToken
    );
}
