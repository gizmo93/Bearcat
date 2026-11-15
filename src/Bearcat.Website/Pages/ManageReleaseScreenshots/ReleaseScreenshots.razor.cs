using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Generation;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Controllers;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseScreenshots;

public partial class ReleaseScreenshots(IScopedOperationRunner operationRunner)
    : ComponentBase,
        IReloadableComponent,
        IAsyncDisposable
{
    private static readonly TimeSpan WaitingStateRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource pollingCancellation = new();
    private Task? pollingTask;

    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    private ReleaseScreenshotOverviewReadModel? overview;
    private bool isLoading;
    private bool isScheduling;

    private bool IsEnabled =>
        overview is { CreateScreenshots: true } or { CreateScreenshotGrid: true };

    private bool IsManagedWithReleaseFolder =>
        overview is { ReleaseType: ReleaseType.Managed, HasReleaseFolder: true };

    private bool CanRegenerate => IsEnabled && IsManagedWithReleaseFolder;

    private bool ShowRegenerateUnavailableHint =>
        IsEnabled
        && !CanRegenerate
        && State is ReleaseScreenshotState.Created or ReleaseScreenshotState.Failed;

    private bool HasUnavailableScreenshots =>
        overview?.Screenshots.Any(screenshot => GetImageUrl(screenshot) is null) ?? false;

    private ReleaseScreenshotState State => GetState();

    private IReadOnlyList<ReleaseScreenshotReadModel> Screenshots =>
        overview
            ?.Screenshots.Where(screenshot => screenshot.Kind is ReleaseScreenshotKind.Screenshot)
            .ToList()
        ?? [];

    private ReleaseScreenshotReadModel? ScreenshotGrid =>
        overview?.Screenshots.FirstOrDefault(screenshot =>
            screenshot.Kind is ReleaseScreenshotKind.ScreenshotGrid
        );

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        pollingTask = PollWhileWaitingAsync(pollingCancellation.Token);
    }

    private async Task PollWhileWaitingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(WaitingStateRefreshInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (State is not ReleaseScreenshotState.Waiting)
                {
                    continue;
                }

                await InvokeAsync(ReloadAsync);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task ReloadAsync()
    {
        isLoading = true;

        try
        {
            overview = await operationRunner.RunAsync(
                (IReleaseScreenshotReadRepository repository) =>
                    repository.GetOverviewAsync(ReleaseId)
            );
        }
        finally
        {
            isLoading = false;
        }

        StateHasChanged();
    }

    private async Task ScheduleRegenerationAsync()
    {
        isScheduling = true;

        try
        {
            await operationRunner.RunAsync(
                (ReleaseScreenshotGenerationService service) =>
                    service.ScheduleRegenerationAsync(ReleaseId)
            );
        }
        finally
        {
            isScheduling = false;
        }

        await ReloadAsync();
    }

    private ReleaseScreenshotState GetState()
    {
        if (overview is null)
        {
            return ReleaseScreenshotState.Loading;
        }

        if (!IsEnabled)
        {
            return ReleaseScreenshotState.Disabled;
        }

        if (overview is { ScreenshotsGeneratedAt: not null, Screenshots.Count: > 0 })
        {
            return ReleaseScreenshotState.Created;
        }

        if (!IsManagedWithReleaseFolder)
        {
            return ReleaseScreenshotState.NeedsManagedRelease;
        }

        if (overview.ScreenshotGenerationErrorMessage is not null)
        {
            return ReleaseScreenshotState.Failed;
        }

        if (overview.ScreenshotsGeneratedAt is not null)
        {
            return ReleaseScreenshotState.Created;
        }

        if (overview.ScreenshotGenerationIsPending)
        {
            return ReleaseScreenshotState.Waiting;
        }

        return overview is { HasMediaFiles: false, MediaMetadataExtractedAt: not null }
            ? ReleaseScreenshotState.NoVideoFile
            : ReleaseScreenshotState.NoImageUploadConfig;
    }

    private static string? GetImageUrl(ReleaseScreenshotReadModel screenshot)
    {
        return screenshot.FileExists
            ? ReleaseScreenshotController.GetUrl(screenshot.ReleaseScreenshotId)
            : screenshot.HostedPreviewUrl;
    }

    private static string? GetFullSizeUrl(ReleaseScreenshotReadModel screenshot)
    {
        return screenshot.FileExists
            ? ReleaseScreenshotController.GetUrl(screenshot.ReleaseScreenshotId)
            : screenshot.HostedFullUrl;
    }

    private static string FormatTimestamp(TimeSpan? timestamp)
    {
        return timestamp?.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await pollingCancellation.CancelAsync();

        if (pollingTask is not null)
        {
            await pollingTask;
        }

        pollingCancellation.Dispose();
    }

    private enum ReleaseScreenshotState
    {
        Loading,
        Disabled,
        NeedsManagedRelease,
        NoVideoFile,
        Waiting,
        NoImageUploadConfig,
        Failed,
        Created,
    }
}
