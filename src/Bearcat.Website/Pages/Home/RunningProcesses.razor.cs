using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Progress;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Website.Pages.Home;

public sealed partial class RunningProcesses(
    IUploadProgressTracker uploadProgressTracker,
    IScopedOperationRunner operationRunner
) : IDisposable
{
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private IReadOnlyList<Upload> runningUploads = [];

    private IReadOnlyDictionary<int, UploadProgressSnapshot> uploadProgress =
        new Dictionary<int, UploadProgressSnapshot>();

    private IReadOnlyList<Archive> runningArchives = [];

    private bool SomethingIsRunning => runningUploads.Count > 0 || runningArchives.Count > 0;

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

        uploadProgress = runningUploads
            .Select(upload => uploadProgressTracker.Get(upload.Id))
            .Where(snapshot => snapshot is not null)
            .ToDictionary(snapshot => snapshot!.UploadId, snapshot => snapshot!);
    }

    private async Task LoadRunningArchivesAsync(
        IBearcatReadDbContext dbRead,
        CancellationToken cancellationToken
    )
    {
        runningArchives = await dbRead
            .Archives.Include(a => a.ArchiveConfig)
                .ThenInclude(ac => ac.Release)
            .Where(a => a.ArchiveState == ArchiveState.Creating)
            .ToListAsync(cancellationToken);
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
