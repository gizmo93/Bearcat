using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Progress;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.Home;

public partial class RunningProcesses(
    NavigationManager navigationManager,
    IUploadProgressTracker uploadProgressTracker
)
{
    private IReadOnlyList<Upload> runningUploads = [];

    private IReadOnlyDictionary<int, UploadProgressSnapshot> uploadProgress =
        new Dictionary<int, UploadProgressSnapshot>();

    private IReadOnlyList<Archive> runningArchives = [];

    private IBearcatReadDbContext dbRead = null!;

    private bool SomethingIsRunning => runningUploads.Count > 0 || runningArchives.Count > 0;

    private bool autoRefresh = true;

    private bool refreshInProgress;

    private bool isDisposed;

    private PeriodicTimer? refreshTimer;

    private CancellationTokenSource? refreshCts;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        dbRead = ScopedServices.GetRequiredService<IBearcatReadDbContext>();
        navigationManager.LocationChanged += OnLocationChanged;
        await LoadDataAsync();
        StartAutoRefreshTimer();
    }

    private async Task LoadRunningUploadsAsync()
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
            .ToListAsync();

        uploadProgress = runningUploads
            .Select(upload => uploadProgressTracker.Get(upload.Id))
            .Where(snapshot => snapshot is not null)
            .ToDictionary(snapshot => snapshot!.UploadId, snapshot => snapshot!);
    }

    private async Task LoadRunningArchivesAsync()
    {
        runningArchives = await dbRead
            .Archives.Include(a => a.ArchiveConfig)
                .ThenInclude(ac => ac.Release)
            .Where(a => a.ArchiveState == ArchiveState.Creating)
            .ToListAsync();
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
        refreshCts = new CancellationTokenSource();
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
                await InvokeAsync(LoadDataAsync);
            }
        }
        catch (Exception)
        {
            // Expected on stop (cancellation) and for disposed DbContexts during teardown
        }
    }

    private async Task LoadDataAsync()
    {
        if (isDisposed)
        {
            return;
        }

        refreshInProgress = true;
        StateHasChanged();

        await LoadRunningUploadsAsync();
        await LoadRunningArchivesAsync();

        refreshInProgress = false;
        StateHasChanged();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        autoRefresh = false;
        StopAutoRefreshTimer();
    }

    private void StopAutoRefreshTimer()
    {
        refreshCts?.Cancel();
        refreshCts?.Dispose();
        refreshCts = null;

        refreshTimer?.Dispose();
        refreshTimer = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            isDisposed = true;
            navigationManager.LocationChanged -= OnLocationChanged;
            StopAutoRefreshTimer();
        }

        base.Dispose(disposing);
    }
}
