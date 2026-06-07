using Bearcat.LinkCrypters.ToLinkTo.Api.EditFolder;
using Refit;
using RequestBody = Bearcat.LinkCrypters.ToLinkTo.Api.Ping.RequestBody;

namespace Bearcat.LinkCrypters.ToLinkTo.Api;

public interface IToLinkToApi
{
    [Post("/api/v1/ping")]
    Task<ApiResponse<string>> PingAsync(
        [Body] ApiRequest<RequestBody> request,
        CancellationToken cancellationToken
    );

    [Post("/api/v1/folder/create")]
    Task<ApiResponse<string>> CreateFolderAsync(
        [Body] ApiRequest<CreateFolder.RequestBody> request,
        CancellationToken cancellationToken
    );

    [Post("/api/v1/folder/edit")]
    Task<ApiResponse<ResponseBody>> EditFolderAsync(
        [Body] ApiRequest<EditFolder.RequestBody> request,
        CancellationToken cancellationToken
    );
}
