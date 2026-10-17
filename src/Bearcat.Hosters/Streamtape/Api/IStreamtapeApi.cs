using Refit;

namespace Bearcat.Hosters.Streamtape.Api;

public interface IStreamtapeApi
{
    [Get("/account/info")]
    Task<StreamtapeResponse> GetAccountInfoAsync(
        [Query] [AliasAs("login")] string apiLogin,
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/file/ul")]
    Task<StreamtapeResponse> GetUploadUrlAsync(
        [Query] [AliasAs("login")] string apiLogin,
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("folder")] string? folderId,
        CancellationToken cancellationToken
    );

    [Get("/file/info")]
    Task<StreamtapeResponse> GetFileInfoAsync(
        [Query] [AliasAs("login")] string apiLogin,
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file")] string fileIds,
        CancellationToken cancellationToken
    );

    [Get("/file/listfolder")]
    Task<StreamtapeResponse> GetRootFolderContentAsync(
        [Query] [AliasAs("login")] string apiLogin,
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/file/createfolder")]
    Task<StreamtapeResponse> CreateFolderAsync(
        [Query] [AliasAs("login")] string apiLogin,
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("name")] string name,
        CancellationToken cancellationToken
    );

    [Get("/file/move")]
    Task<StreamtapeResponse> MoveFileToFolderAsync(
        [Query] [AliasAs("login")] string apiLogin,
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file")] string fileId,
        [Query] [AliasAs("folder")] string folderId,
        CancellationToken cancellationToken
    );
}
