using Bearcat.Hosters.FileFactory.Api.Requests;
using Bearcat.Hosters.FileFactory.Api.Responses;
using Refit;

namespace Bearcat.Hosters.FileFactory.Api;

public interface IFileFactoryConvexApi
{
    [Post("/api/query")]
    Task<ApiResponse<ConvexResponse<CurrentUserResponse>>> QueryCurrentUserAsync(
        [Authorize("Bearer")] string token,
        [Body] ConvexQueryRequest request,
        CancellationToken cancellationToken
    );
}
