using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Website.Formatting;
using Bearcat.Website.Pages.ManageReleases;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Overview;

public partial class ReleaseCollectionReleases(
    DialogService dialogService,
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager,
    TimeProvider timeProvider
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int ReleaseCollectionId { get; set; }

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ReleaseCollectionReleaseReadModel> Releases { get; set; } = [];

    [Parameter]
    public EventCallback OnReleasesChanged { get; set; }

    private Dictionary<int, string?> episodeLabelsByReleaseId = [];

    protected override void OnParametersSet()
    {
        var episodeLabels = ReleaseCollectionEpisodeLabel.GetEpisodeLabels(
            Releases.Select(release => release.Name).ToList()
        );
        episodeLabelsByReleaseId = Releases
            .Zip(episodeLabels)
            .ToDictionary(pair => pair.First.ReleaseId, pair => pair.Second);
    }

    private static DateTime? GetLatestUploadAt(ReleaseCollectionReleaseReadModel release) =>
        release
            .LatestUploads.Select(upload => (DateTime?)(upload.UploadedAt ?? upload.CreatedAt))
            .Max();

    private string? GetNotFullyOnlineSinceTitle(ReleaseCollectionReleaseReadModel release) =>
        release.NotFullyOnlineSince is { } notFullyOnlineSince
            ? $"{L["NotFullyOnlineSince"]}: {HumanizeTimestamp(notFullyOnlineSince)} ({FormatTimestamp(notFullyOnlineSince)})"
            : null;

    private string HumanizeTimestamp(DateTime value) => timeProvider.Humanize(value);

    private static string FormatTimestamp(DateTime value) =>
        value.ToString("g", CultureInfo.CurrentCulture);

    private static string GetReleaseUrl(ReleaseCollectionReleaseReadModel release) =>
        $"/releases/{release.ReleaseId}";

    private void NavigateToRelease(ReleaseCollectionReleaseReadModel release) =>
        navigationManager.NavigateTo(GetReleaseUrl(release));

    private async Task ShowAddReleaseDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AddReleaseToCollectionDialog.ReleaseCollectionId)] = ReleaseCollectionId,
        };

        var dialog = await dialogService.OpenAsync<AddReleaseToCollectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddRelease"],
                Description = L["AddReleaseToCollectionDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await OnReleasesChanged.InvokeAsync();
        }
    }

    private async Task ShowUploadLinksDialogAsync(
        ReleaseCollectionReleaseReadModel release,
        ReleaseLatestUploadReadModel upload
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(UploadLinksDialog.ReleaseId)] = release.ReleaseId,
            [nameof(UploadLinksDialog.UploadId)] = upload.UploadId,
            [nameof(UploadLinksDialog.UploadConfigName)] = upload.UploadConfigName,
        };

        await dialogService.OpenAsync<UploadLinksDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["UploadLinksTitle", upload.UploadId],
                Description = L["UploadLinksDialogDescription", upload.UploadConfigName],
                Size = DialogSize.Full,
                ShowClose = true,
            }
        );
    }

    private async Task RemoveReleaseAsync(ReleaseCollectionReleaseReadModel release)
    {
        var result = await dialogService.ConfirmAsync(
            L["RemoveFromCollection"],
            L["RemoveReleaseFromCollectionConfirmation", release.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Remove"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await operationRunner.RunAsync(
            (ReleaseCollectionService service) =>
                service.RemoveReleaseAsync(ReleaseCollectionId, release.ReleaseId)
        );
        await OnReleasesChanged.InvokeAsync();
    }
}
