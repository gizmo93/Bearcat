using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Dto;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class RemoteSourceDownloadReadRepository(IBearcatReadDbContext dbRead)
    : IRemoteSourceDownloadReadRepository
{
    private static readonly RemoteSourceDownloadState[] RunningStates =
    [
        RemoteSourceDownloadState.Pending,
        RemoteSourceDownloadState.Downloading,
        RemoteSourceDownloadState.Downloaded,
        RemoteSourceDownloadState.Verifying,
        RemoteSourceDownloadState.Extracting,
        RemoteSourceDownloadState.ReadyForReleaseCreation,
    ];

    private static readonly RemoteSourceDownloadState[] VerifyingOrExtractingStates =
    [
        RemoteSourceDownloadState.Verifying,
        RemoteSourceDownloadState.Extracting,
    ];

    private static readonly RemoteSourceDownloadState[] DownloadedAndWaitingForNextStepStates =
    [
        RemoteSourceDownloadState.Downloaded,
        RemoteSourceDownloadState.ReadyForReleaseCreation,
    ];

    public async Task<PagedResult<RemoteSourceDownloadReadModel>> SearchAsync(
        RemoteSourceDownloadSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);
        var downloadsQuery = dbRead.RemoteSourceDownloads.Where(download =>
            query.States.Contains(download.State)
        );

        var totalCount = await downloadsQuery.CountAsync(cancellationToken);

        var downloads = await downloadsQuery
            .OrderByDescending(download => download.DiscoveredAt)
            .ThenByDescending(download => download.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(ToReadModel())
            .ToListAsync(cancellationToken);

        return new PagedResult<RemoteSourceDownloadReadModel>(
            downloads,
            totalCount,
            pageIndex,
            pageSize
        );
    }

    public async Task<IReadOnlyList<RemoteSourceDownloadReadModel>> GetRunningAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .RemoteSourceDownloads.Where(download => RunningStates.Contains(download.State))
            .OrderBy(download =>
                download.State == RemoteSourceDownloadState.Downloading ? 0
                : VerifyingOrExtractingStates.Contains(download.State) ? 1
                : DownloadedAndWaitingForNextStepStates.Contains(download.State) ? 2
                : 3
            )
            .ThenBy(download => download.DiscoveredAt)
            .ThenBy(download => download.Id)
            .Select(ToReadModel())
            .ToListAsync(cancellationToken);
    }

    public async Task<RemoteSourceDownloadReadModel?> GetByReleaseIdAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .RemoteSourceDownloads.Where(download => download.ReleaseId == releaseId)
            .OrderByDescending(download => download.CompletedAt)
            .ThenByDescending(download => download.Id)
            .Select(ToReadModel())
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static Expression<
        Func<RemoteSourceDownload, RemoteSourceDownloadReadModel>
    > ToReadModel()
    {
        return download => new RemoteSourceDownloadReadModel(
            download.Id,
            download.SourceName,
            download.RemoteSourceAutomation == null ? null : download.RemoteSourceAutomation.Name,
            download.RemoteFolderPath,
            download.FolderName,
            download.LocalFolderPath,
            download.State,
            download.FileCount,
            download.TotalBytes,
            download.DiscoveredAt,
            download.StartedAt,
            download.CompletedAt,
            download.ErrorMessage,
            download.ReleaseId,
            download.Release == null ? null : download.Release.Name
        );
    }
}
