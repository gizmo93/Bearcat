using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Dto;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.ReadModels;

namespace Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Repositories;

public interface IRemoteSourceDownloadReadRepository
{
    Task<PagedResult<RemoteSourceDownloadReadModel>> SearchAsync(
        RemoteSourceDownloadSearchQuery query,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<RemoteSourceDownloadReadModel>> GetRunningAsync(
        CancellationToken cancellationToken = default
    );

    Task<RemoteSourceDownloadReadModel?> GetByReleaseIdAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );
}
