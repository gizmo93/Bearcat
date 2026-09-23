using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Website.ScopedOperations;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Website.Pages.Home;

public sealed partial class RunningProcesses(
    ITransferProgressTracker transferProgressTracker,
    IScopedOperationRunner operationRunner
) : IDisposable
{
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private IReadOnlyList<Upload> runningUploads = [];

    private IReadOnlyDictionary<int, TransferProgressSnapshot> uploadProgress =
        new Dictionary<int, TransferProgressSnapshot>();

    private IReadOnlyList<Archive> creatingArchives = [];

    private IReadOnlyList<Archive> restoringArchives = [];

    private IReadOnlyDictionary<int, TransferProgressSnapshot> downloadProgress =
        new Dictionary<int, TransferProgressSnapshot>();

    private IReadOnlyList<RemoteSourceDownloadReadModel> remoteDownloads = [];

    private IReadOnlyDictionary<int, TransferProgressSnapshot> remoteDownloadProgress =
        new Dictionary<int, TransferProgressSnapshot>();

    private bool SomethingIsRunning =>
        runningUploads.Count > 0
        || creatingArchives.Count > 0
        || restoringArchives.Count > 0
        || remoteDownloads.Count > 0;

    private bool autoRefresh = true;

    private bool refreshInProgress;

    private bool isDisposed;

    private PeriodicTimer? refreshTimer;

    private CancellationTokenSource? refreshCts;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync(lifetimeCancellation.Token);
        if (!isDisposed)
        {
            StartAutoRefreshTimer();
        }
    }

    private async Task LoadRunningUploadsAsync(
        IBearcatReadDbContext dbRead,
        CancellationToken cancellationToken
    )
    {
        runningUploads = await dbRead
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadedFiles)
            .Include(u => u.Archive)
                .ThenInclude(a => a!.ArchiveFiles)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
            .Include(u => u.UploadConfig)
                .ThenInclude(u => u.HosterRegistration)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.ArchiveConfig)
            .Where(u =>
                u.UploadState == UploadState.Pending
                || u.UploadState == UploadState.Uploading
                || u.UploadState == UploadState.CancellationRequested
            )
            .ToListAsync(cancellationToken);

        uploadProgress = GetProgressSnapshots(
            TransferKind.Upload,
            runningUploads.Select(upload => upload.Id).ToList()
        );
    }

    private async Task LoadRunningArchivesAsync(
        IBearcatReadDbContext dbRead,
        CancellationToken cancellationToken
    )
    {
        var archives = await dbRead
            .Archives.Include(a => a.ArchiveConfig)
                .ThenInclude(ac => ac.Release)
            .Where(a =>
                a.ArchiveState == ArchiveState.Creating || a.ArchiveState == ArchiveState.Restoring
            )
            .ToListAsync(cancellationToken);

        creatingArchives = archives
            .Where(archive => archive.ArchiveState == ArchiveState.Creating)
            .ToList();

        restoringArchives = archives
            .Where(archive => archive.ArchiveState == ArchiveState.Restoring)
            .ToList();

        downloadProgress = GetProgressSnapshots(
            TransferKind.MirrorDownload,
            restoringArchives.Select(archive => archive.Id).ToList()
        );
    }

    private async Task LoadRemoteDownloadsAsync(CancellationToken cancellationToken)
    {
        remoteDownloads = await operationRunner.RunAsync<
            IRemoteSourceDownloadReadRepository,
            IReadOnlyList<RemoteSourceDownloadReadModel>
        >((repository, token) => repository.GetRunningAsync(token), cancellationToken);

        remoteDownloadProgress = GetProgressSnapshots(
            TransferKind.RemoteDownload,
            remoteDownloads.Select(download => download.Id).ToList()
        );
    }

    private Dictionary<int, TransferProgressSnapshot> GetProgressSnapshots(
        TransferKind kind,
        IReadOnlyList<int> ids
    )
    {
        return ids.Select(id => transferProgressTracker.Get(new TransferKey(kind, id)))
            .OfType<TransferProgressSnapshot>()
            .ToDictionary(snapshot => snapshot.Key.Id);
    }

    private void ToggleAutoRefresh()
    {
        if (autoRefresh)
        {
            autoRefresh = false;
            StopAutoRefreshTimer();
            return;
        }

        autoRefresh = true;
        StartAutoRefreshTimer();
    }

    private void StartAutoRefreshTimer()
    {
        refreshCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
        refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        _ = RunAutoRefreshLoopAsync(refreshTimer, refreshCts.Token);
    }

    private async Task RunAutoRefreshLoopAsync(
        PeriodicTimer timer,
        CancellationToken cancellationToken
    )
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(() => LoadDataAsync(cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private Task LoadDataAsync() => LoadDataAsync(lifetimeCancellation.Token);

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        if (isDisposed || refreshInProgress)
        {
            return;
        }

        refreshInProgress = true;
        StateHasChanged();

        try
        {
            await operationRunner.RunAsync<IBearcatReadDbContext>(
                async (dbRead, operationCancellationToken) =>
                {
                    await LoadRunningUploadsAsync(dbRead, operationCancellationToken);
                    await LoadRunningArchivesAsync(dbRead, operationCancellationToken);
                },
                cancellationToken
            );
            await LoadRemoteDownloadsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            refreshInProgress = false;
        }

        if (isDisposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        StateHasChanged();
    }

    private void StopAutoRefreshTimer()
    {
        refreshCts?.Cancel();
        refreshCts?.Dispose();
        refreshCts = null;

        refreshTimer?.Dispose();
        refreshTimer = null;
    }

    public void Dispose()
    {
        isDisposed = true;
        lifetimeCancellation.Cancel();
        StopAutoRefreshTimer();
        lifetimeCancellation.Dispose();
    }
}
