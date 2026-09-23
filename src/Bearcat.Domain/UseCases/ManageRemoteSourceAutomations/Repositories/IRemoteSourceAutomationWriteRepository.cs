using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageRemoteSourceAutomations.Repositories;

public interface IRemoteSourceAutomationWriteRepository
{
    Task<RemoteSourceAutomation> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<bool> RegistrationExistsAsync(
        int remoteSourceRegistrationId,
        CancellationToken cancellationToken = default
    );

    Task<bool> ReleaseTemplateExistsAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<RemoteSourceDownload>> GetObservingDownloadsAsync(
        int remoteSourceAutomationId,
        CancellationToken cancellationToken = default
    );

    void Add(RemoteSourceAutomation automation);

    void Remove(RemoteSourceAutomation automation);

    void Remove(RemoteSourceDownload download);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
