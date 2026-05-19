using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Timer = System.Timers.Timer;

namespace Bearcat.Website.Pages.Home;

public partial class RunningProcesses(NavigationManager navigationManager)
{
    private IReadOnlyList<Upload> runningUploads = [];

    private IReadOnlyList<Archive> runningArchives = [];

    private IBearcatReadDbContext dbRead = null!;

    private bool SomethingIsRunning => runningUploads.Count > 0 || runningArchives.Count > 0;

    private bool autoRefresh;

    private bool refreshInProgress;

    private Timer? refreshTimer;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        dbRead = ScopedServices.GetRequiredService<IBearcatReadDbContext>();
        await LoadDataAsync();

        navigationManager.LocationChanged += (_, _) =>
        {
            if (!autoRefresh || refreshTimer is null)
            {
                return;
            }

            refreshTimer.Stop();
            refreshTimer.Dispose();
            refreshTimer = null;
        };
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

            if (refreshTimer is null)
            {
                return;
            }

            refreshTimer.Stop();
            refreshTimer.Dispose();
            refreshTimer = null;
            return;
        }

        autoRefresh = true;
        refreshTimer = new Timer(TimeSpan.FromSeconds(3));
        refreshTimer.Elapsed += async (_, _) =>
        {
            await InvokeAsync(async () =>
            {
                if (!refreshInProgress)
                {
                    await LoadDataAsync();
                }
            });
        };
        refreshTimer.AutoReset = true;
        refreshTimer.Start();
    }

    private async Task LoadDataAsync()
    {
        refreshInProgress = true;
        StateHasChanged();

        await LoadRunningUploadsAsync();
        await LoadRunningArchivesAsync();

        refreshInProgress = false;
        StateHasChanged();
    }
}
