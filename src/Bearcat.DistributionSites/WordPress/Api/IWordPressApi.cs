using Refit;

namespace Bearcat.DistributionSites.WordPress.Api;

public interface IWordPressApi
{
    [Get("/index.php?rest_route=/wp/v2/users/me&context=edit")]
    Task<ApiResponse<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken);

    [Get(
        "/index.php?rest_route=/wp/v2/categories&per_page={perPage}&_fields=id,name,parent&page={page}"
    )]
    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(
        int perPage,
        int page,
        CancellationToken cancellationToken
    );

    [Post("/index.php?rest_route=/wp/v2/media")]
    Task<MediaResponse> UploadMediaAsync(
        [Body] HttpContent content,
        CancellationToken cancellationToken
    );

    [Post("/index.php?rest_route=/wp/v2/posts")]
    Task<PostResponse> CreatePostAsync(
        [Body] CreatePostRequest request,
        CancellationToken cancellationToken
    );

    [Post("/index.php?rest_route=/wp/v2/posts/{id}")]
    Task<PostResponse> EditPostAsync(
        int id,
        [Body] EditPostRequest request,
        CancellationToken cancellationToken
    );
}
