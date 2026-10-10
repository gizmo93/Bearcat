using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Repositories;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.Home.Summary;
using Bearcat.Website.ScopedOperations;

namespace Bearcat.Website.Pages.Home;

public sealed partial class RunningProcesses(
    ITransferProgressTracker transferProgressTracker,
    IScopedOperationRunner operationRunner
) : IDisposable
{
    private const int SpeedHistoryCapacity = 40;

    private readonly CancellationTokenSource lifetimeCancellation = new();

    private readonly TransferSpeedHistory uploadSpeedHistory = new(SpeedHistoryCapacity);

    private readonly TransferSpeedHistory downloadSpeedHistory = new(SpeedHistoryCapacity);

    private IReadOnlyList<RunningUploadReadModel> runningUploads = [];

    private IReadOnlyDictionary<int, TransferProgressSnapshot> uploadProgress =
        new Dictionary<int, TransferProgressSnapshot>();

    private IReadOnlyList<RunningArchiveReadModel> archivesInCreationOrHashChange = [];

    private IReadOnlyDictionary<int, TransferProgressSnapshot> archiveCreationOrHashChangeProgress =
        new Dictionary<int, TransferProgressSnapshot>();

    private IReadOnlyList<RunningArchiveReadModel> restoringArchives = [];

    private IReadOnlyList<RunningArchiveReadModel> archivesMovingToStorageFolder = [];

    private IReadOnlyDictionary<int, TransferProgressSnapshot> archiveStorageFolderMoveProgress =
        new Dictionary<int, TransferProgressSnapshot>();

    private IReadOnlyList<RunningArchiveReadModel> archivesCopiedIntoLocalWorkingCopy = [];

    private IReadOnlyDictionary<int, TransferProgressSnapshot> archiveLocalWorkingCopyProgress =
        new Dictionary<int, TransferProgressSnapshot>();

    private IReadOnlyDictionary<int, TransferProgressSnapshot> downloadProgress =
        new Dictionary<int, TransferProgressSnapshot>();

    private IReadOnlyList<RemoteSourceDownloadReadModel> remoteDownloads = [];

    private IReadOnlyDictionary<int, TransferProgressSnapshot> remoteDownloadProgress =
        new Dictionary<int, TransferProgressSnapshot>();

    private IReadOnlyList<TransferProgressSnapshot> releaseFolderExtractionProgress = [];

    private bool SomethingIsRunning =>
        runningUploads.Count > 0
        || archivesInCreationOrHashChange.Count > 0
        || restoringArchives.Count > 0
        || archivesMovingToStorageFolder.Count > 0
        || archivesCopiedIntoLocalWorkingCopy.Count > 0
        || remoteDownloads.Count > 0
        || releaseFolderExtractionProgress.Count > 0;

    private int QueuedUploadCount =>
        runningUploads.Count(upload => upload.UploadState is UploadState.Pending);

    private int RunningCount =>
        runningUploads.Count
        - QueuedUploadCount
        + archivesInCreationOrHashChange.Count
        + restoringArchives.Count
        + archivesMovingToStorageFolder.Count
        + archivesCopiedIntoLocalWorkingCopy.Count
        + remoteDownloads.Count
        + releaseFolderExtractionProgress.Count;

    private double TotalUploadBytesPerSecond => SumBytesPerSecond(uploadProgress.Values.ToList());

    private double TotalDownloadBytesPerSecond =>
        SumBytesPerSecond([
            .. downloadProgress.Values,
            .. remoteDownloadProgress.Values.Where(snapshot =>
                snapshot.Identifier.Type is TransferType.RemoteDownload
            ),
        ]);

    private string StatusText
    {
        get
        {
            List<string> parts = [];

            if (RunningCount > 0)
            {
                parts.Add(
                    RunningCount == 1
                        ? L["ActivityRunningCountOne"]
                        : L["ActivityRunningCount", RunningCount]
                );
            }

            if (QueuedUploadCount > 0)
            {
                parts.Add(
                    QueuedUploadCount == 1
                        ? L["ActivityQueuedCountOne"]
                        : L["ActivityQueuedCount", QueuedUploadCount]
                );
            }

            return parts.Count == 0 ? L["ActivityNothingRunning"] : string.Join(" · ", parts);
        }
    }

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

    private async Task LoadRunningUploadsAsync(CancellationToken cancellationToken)
    {
        runningUploads = await operationRunner.RunAsync<
            IUploadReadRepository,
            IReadOnlyList<RunningUploadReadModel>
        >((repository, token) => repository.GetRunningUploadsAsync(token), cancellationToken);

        uploadProgress = GetProgressSnapshots(
            TransferType.Upload,
            runningUploads.Select(upload => upload.UploadId).ToList()
        );
    }

    private async Task LoadRunningArchivesAsync(CancellationToken cancellationToken)
    {
        var archiveIdsWithHashChange = transferProgressTracker.GetTrackedIds(
            TransferType.ArchiveHashChange
        );
        var archiveIdsMovingToStorageFolder = transferProgressTracker.GetTrackedIds(
            TransferType.ArchiveMoveToStorageFolder
        );
        var archiveIdsCopiedIntoLocalWorkingCopy = transferProgressTracker.GetTrackedIds(
            TransferType.ArchiveCopyIntoLocalWorkingCopy
        );

        var archives = await operationRunner.RunAsync<
            IArchiveReadRepository,
            IReadOnlyList<RunningArchiveReadModel>
        >(
            (repository, token) =>
                repository.GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync(
                    [
                        .. archiveIdsWithHashChange,
                        .. archiveIdsMovingToStorageFolder,
                        .. archiveIdsCopiedIntoLocalWorkingCopy,
                    ],
                    token
                ),
            cancellationToken
        );

        archivesInCreationOrHashChange = archives
            .Where(archive =>
                archive.ArchiveState == ArchiveState.Creating
                || archiveIdsWithHashChange.Contains(archive.ArchiveId)
            )
            .ToList();

        restoringArchives = archives
            .Where(archive => archive.ArchiveState == ArchiveState.Restoring)
            .ToList();

        archivesMovingToStorageFolder = archives
            .Where(archive =>
                archive.ArchiveState == ArchiveState.Created
                && archiveIdsMovingToStorageFolder.Contains(archive.ArchiveId)
            )
            .ToList();

        archivesCopiedIntoLocalWorkingCopy = archives
            .Where(archive =>
                archive.ArchiveState == ArchiveState.Created
                && archiveIdsCopiedIntoLocalWorkingCopy.Contains(archive.ArchiveId)
            )
            .ToList();

        archiveCreationOrHashChangeProgress = GetProgressSnapshotsOfFirstTrackedType(
            [
                TransferType.ArchiveCreation,
                TransferType.ArchiveHashing,
                TransferType.ArchiveHashChange,
            ],
            archivesInCreationOrHashChange.Select(archive => archive.ArchiveId).ToList()
        );

        downloadProgress = GetProgressSnapshots(
            TransferType.MirrorDownload,
            restoringArchives.Select(archive => archive.ArchiveId).ToList()
        );

        archiveStorageFolderMoveProgress = GetProgressSnapshots(
            TransferType.ArchiveMoveToStorageFolder,
            archivesMovingToStorageFolder.Select(archive => archive.ArchiveId).ToList()
        );

        archiveLocalWorkingCopyProgress = GetProgressSnapshots(
            TransferType.ArchiveCopyIntoLocalWorkingCopy,
            archivesCopiedIntoLocalWorkingCopy.Select(archive => archive.ArchiveId).ToList()
        );
    }

    private async Task LoadRemoteDownloadsAsync(CancellationToken cancellationToken)
    {
        remoteDownloads = await operationRunner.RunAsync<
            IRemoteSourceDownloadReadRepository,
            IReadOnlyList<RemoteSourceDownloadReadModel>
        >((repository, token) => repository.GetRunningAsync(token), cancellationToken);

        remoteDownloadProgress = GetProgressSnapshotsOfFirstTrackedType(
            [
                TransferType.RemoteDownload,
                TransferType.RemoteDownloadVerification,
                TransferType.RemoteDownloadExtraction,
            ],
            remoteDownloads.Select(download => download.Id).ToList()
        );
    }

    private void LoadRunningReleaseFolderExtractions()
    {
        List<TransferType> types =
        [
            TransferType.ReleaseFolderVerification,
            TransferType.ReleaseFolderExtraction,
        ];

        var releaseFolderObservationIds = types
            .SelectMany(transferProgressTracker.GetTrackedIds)
            .Distinct()
            .Order()
            .ToList();

        releaseFolderExtractionProgress = GetProgressSnapshotsOfFirstTrackedType(
            types,
            releaseFolderObservationIds
        )
            .Values.OrderBy(snapshot => snapshot.Identifier.Id)
            .ToList();
    }

    private Dictionary<int, TransferProgressSnapshot> GetProgressSnapshotsOfFirstTrackedType(
        IReadOnlyList<TransferType> types,
        IReadOnlyList<int> ids
    )
    {
        return ids.Select(id =>
                types
                    .Select(type => transferProgressTracker.Get(new TransferIdentifier(type, id)))
                    .FirstOrDefault(snapshot => snapshot is not null)
            )
            .OfType<TransferProgressSnapshot>()
            .ToDictionary(snapshot => snapshot.Identifier.Id);
    }

    private Dictionary<int, TransferProgressSnapshot> GetProgressSnapshots(
        TransferType type,
        IReadOnlyList<int> ids
    )
    {
        return ids.Select(id => transferProgressTracker.Get(new TransferIdentifier(type, id)))
            .OfType<TransferProgressSnapshot>()
            .ToDictionary(snapshot => snapshot.Identifier.Id);
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
            await LoadRunningUploadsAsync(cancellationToken);
            await LoadRunningArchivesAsync(cancellationToken);
            await LoadRemoteDownloadsAsync(cancellationToken);
            LoadRunningReleaseFolderExtractions();
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

        uploadSpeedHistory.Add(TotalUploadBytesPerSecond);
        downloadSpeedHistory.Add(TotalDownloadBytesPerSecond);
        StateHasChanged();
    }

    private static double SumBytesPerSecond(IReadOnlyList<TransferProgressSnapshot> snapshots)
    {
        return snapshots
            .Select(snapshot => snapshot.BytesPerSecond)
            .Where(speed => speed > 0)
            .Sum();
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
