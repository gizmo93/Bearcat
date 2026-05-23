using Bearcat.LinkCrypters.HideCx.Api.CreateContainer;
using Refit;

namespace Bearcat.LinkCrypters.HideCx.Api;

public interface IHideCxApi
{
    [Post("/containers-sync")]
    Task<Response> CreateContainerAsync(
        Request request,
        [Header("Authorization")] string apiToken,
        CancellationToken cancellationToken
    );

    [Post("/containers/search")]
    Task<SearchContainers.Response> SearchContainersAsync(
        SearchContainers.Request request,
        [Header("Authorization")] string apiToken,
        CancellationToken cancellationToken
    );

    [Patch("/containers/{containerId}")]
    Task<string?> UpdateContainerAsync(
        string containerId,
        [Body] UpdateContainer.Request request,
        [Header("Authorization")] string apiToken,
        CancellationToken cancellationToken
    );
}
