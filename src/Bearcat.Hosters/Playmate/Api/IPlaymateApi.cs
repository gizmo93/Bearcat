using Refit;

namespace Bearcat.Hosters.Playmate.Api;

public interface IPlaymateApi
{
    [Get("/account/info")]
    Task<PlaymateResponse> GetAccountInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/upload/server")]
    Task<PlaymateResponse> GetUploadServerAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/file/info")]
    Task<PlaymateResponse> GetFileInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query(CollectionFormat.Csv)] [AliasAs("file_code")] IReadOnlyList<string> fileCodes,
        CancellationToken cancellationToken
    );

    [Get("/folder/list")]
    Task<PlaymateResponse> GetFolderListAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/folder/create")]
    Task<PlaymateResponse> CreateFolderAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("name")] string name,
        CancellationToken cancellationToken
    );

    [Get("/file/set_folder")]
    Task<PlaymateResponse> MoveFileToFolderAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file_code")] string fileCode,
        [Query] [AliasAs("fld_id")] string folderId,
        CancellationToken cancellationToken
    );
}
