using Refit;

namespace Bearcat.Hosters.MixDrop.Api;

public interface IMixDropApi
{
    [Get("/folderlist")]
    Task<MixDropResponse> GetFolderListAsync(
        [Query] [AliasAs("email")] string emailAddress,
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("page")] int? page,
        CancellationToken cancellationToken
    );

    [Get("/foldercreate")]
    Task<MixDropResponse> CreateFolderAsync(
        [Query] [AliasAs("email")] string emailAddress,
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("title")] string title,
        CancellationToken cancellationToken
    );

    [Get("/filemove")]
    Task<MixDropResponse> MoveFilesAsync(
        [Query] [AliasAs("email")] string emailAddress,
        [Query] [AliasAs("key")] string apiKey,
        [Query(CollectionFormat.Multi)] [AliasAs("ref[]")] IReadOnlyList<string> fileReferences,
        [Query] [AliasAs("folder")] string folderId,
        CancellationToken cancellationToken
    );

    [Get("/fileinfo2")]
    Task<MixDropResponse> GetFileInfoAsync(
        [Query] [AliasAs("email")] string emailAddress,
        [Query] [AliasAs("key")] string apiKey,
        [Query(CollectionFormat.Multi)] [AliasAs("ref[]")] IReadOnlyList<string> fileReferences,
        CancellationToken cancellationToken
    );
}
