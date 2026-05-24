using Bearcat.Hosters.Fichier.Api.File;
using Bearcat.Hosters.Fichier.Api.User;
using Refit;

namespace Bearcat.Hosters.Fichier.Api;

public interface IFichierApi
{
    [Post("/file/info.cgi")]
    Task<ApiResponse<FileInfoResponse>> GetFileInfoAsync(
        [Header("Authorization")] string authorization,
        [Body] FileInfoRequest request,
        CancellationToken cancellationToken
    );

    [Post("/user/info.cgi")]
    Task<ApiResponse<UserInfoResponse>> GetUserInfoAsync(
        [Header("Authorization")] string authorization,
        [Body] UserInfoRequest request,
        CancellationToken cancellationToken
    );
}
