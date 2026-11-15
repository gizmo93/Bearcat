using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Generation;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseScreenshots;

public partial class ReleaseScreenshots(IScopedOperationRunner operationRunner)
    : ComponentBase,
        IReloadableComponent
{
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

        return overview is { HasMediaFiles: false, MediaMetadataExtractedAt: not null }
            ? ReleaseScreenshotState.NoVideoFile
            : ReleaseScreenshotState.Waiting;
    }

    private static string FormatTimestamp(TimeSpan? timestamp)
    {
        return timestamp?.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private enum ReleaseScreenshotState
    {
        Loading,
        Disabled,
        NeedsManagedRelease,
        NoVideoFile,
        Waiting,
        Failed,
        Created,
    }
}
