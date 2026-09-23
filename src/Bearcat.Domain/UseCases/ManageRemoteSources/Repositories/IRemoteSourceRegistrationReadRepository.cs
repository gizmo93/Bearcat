using Bearcat.Domain.UseCases.ManageRemoteSources.ReadModels;

namespace Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;

public interface IRemoteSourceRegistrationReadRepository
{
    Task<IReadOnlyList<RemoteSourceRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );
}
