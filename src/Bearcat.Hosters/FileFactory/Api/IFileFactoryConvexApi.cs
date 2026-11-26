using Bearcat.Hosters.FileFactory.Api.Requests;
using Bearcat.Hosters.FileFactory.Api.Responses;
using Refit;

namespace Bearcat.Hosters.FileFactory.Api;

public interface IFileFactoryConvexApi
{
    [Post("/api/query")]
    Task<ApiResponse<ConvexResponse<CurrentUserResponse>>> QueryCurrentUserAsync(
        [Authorize("Bearer")] string token,
        [Body] ConvexRequest<EmptyArguments> request,
        CancellationToken cancellationToken
    );

    [Post("/api/query")]
    Task<
        ApiResponse<ConvexResponse<List<FolderSelectOptionResponse>>>
    > QueryFolderSelectOptionsAsync(
        [Authorize("Bearer")] string token,
        [Body] ConvexRequest<UserIdArguments> request,
        CancellationToken cancellationToken
    );

    [Post("/api/mutation")]
    Task<ApiResponse<ConvexResponse<string>>> CreateFolderAsync(
        [Authorize("Bearer")] string token,
        [Body] ConvexRequest<CreateFolderArguments> request,
        CancellationToken cancellationToken
    );

    [Post("/api/mutation")]
    Task<ApiResponse<ConvexResponse<object>>> MoveFilesAsync(
        [Authorize("Bearer")] string token,
        [Body] ConvexRequest<MoveFilesArguments> request,
        CancellationToken cancellationToken
    );

    [Post("/api/query")]
    Task<ApiResponse<ConvexResponse<FilePageResponse>>> QueryFilesAsync(
        [Authorize("Bearer")] string token,
        [Body] ConvexRequest<ListFilesArguments> request,
        CancellationToken cancellationToken
    );

    [Post("/api/mutation")]
    Task<ApiResponse<ConvexResponse<object>>> SoftDeleteFolderAsync(
        [Authorize("Bearer")] string token,
        [Body] ConvexRequest<FolderIdArguments> request,
        CancellationToken cancellationToken
    );
}
