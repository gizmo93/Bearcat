using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.ReadModels;

namespace Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.Repositories;

public interface IRemoteSourceAutomationReadRepository
{
    Task<IReadOnlyList<RemoteSourceAutomationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );
}
