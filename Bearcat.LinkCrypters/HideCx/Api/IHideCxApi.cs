using Refit;

namespace Bearcat.LinkCrypters.HideCx.Api;

public interface IHideCxApi
{
    [Post("/containers-sync")]
    Task<CreateContainer.Response> CreateContainerAsync(
        CreateContainer.Request request,
        [Header("Authorization")] string apiToken,
        CancellationToken cancellationToken);

    [Post("/containers/search")]
    Task<SearchContainers.Response> SearchContainersAsync(
        SearchContainers.Request request,
        [Header("Authorization")] string apiToken,
        CancellationToken cancellationToken);

    [Patch("/containers/{containerId}")]
    Task<string?> UpdateContainerAsync(
        [Query] string containerId,
        UpdateContainer.Request request,
        [Header("Authorization")] string apiToken,
        CancellationToken cancellationToken);
}
