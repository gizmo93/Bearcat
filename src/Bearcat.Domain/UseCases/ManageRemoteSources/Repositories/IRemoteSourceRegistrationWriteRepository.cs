using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;

public interface IRemoteSourceRegistrationWriteRepository
{
    Task<RemoteSourceRegistration> GetByIdAsync(int id, CancellationToken cancellationToken);

    void Add(RemoteSourceRegistration registration);

    void Remove(RemoteSourceRegistration registration);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
