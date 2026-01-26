using Refit;

namespace Bearcat.LinkCrypters.HideCx.ApiClient;

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
}
