using Refit;

namespace Bearcat.LinkCrypters.HideCx.ApiClient;

public interface IHideCxApi
{
    [Post("/containers-sync")]
    Task<CreateContainer.Response> CreateContainerAsync(
        CreateContainer.Request request,
        [Header("Bearer")]string apiKey,
        CancellationToken cancellationToken);
}
