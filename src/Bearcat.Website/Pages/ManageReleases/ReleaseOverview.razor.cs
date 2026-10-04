using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Formatting;
using Bearcat.Website.Pages.ManageReleases.DetailTabs;
using Bearcat.Website.Pages.ManageReleases.Overview;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseOverview(
    ToastService toastService,
    DialogService dialogService,
    IScopedOperationRunner operationRunner,
    IJSRuntime jsRuntime,
    NavigationManager navigationManager,
    TimeProvider timeProvider
) : ComponentBase, IReloadableComponent
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    [Parameter]
    [EditorRequired]
    public string ReleaseName { get; set; } = null!;

    [Parameter]
    public string? ReleaseFolderPath { get; set; }

    [Parameter]
    public EventCallback OnRefreshed { get; set; }

    private IReadOnlyList<ReleaseOverviewUploadReadModel> overviewUploads = [];
    private IReadOnlyList<ReleaseOverviewImageUploadReadModel> overviewImageUploads = [];
    private ReleaseOverviewSummary summary = ReleaseOverviewSummaryCalculator.Calculate([]);
    private IReadOnlyList<LinkCrypterContainerUrls> containerUrlsByLinkCrypter = [];
    private readonly HashSet<int> expandedUploadConfigIds = [];
    private ReleaseNfoReadModel? releaseNfo;
    private string? nfoContent;
    private bool hasLocalNfo;
    private bool isLoading;
    private int? loadedReleaseId;
    private string? loadedReleaseFolderPath;
    private bool CanCopyNfo => !isLoading && !string.IsNullOrEmpty(nfoContent);
    private bool CanSaveNfoFile =>
        !isLoading
        && releaseNfo is not null
        && !hasLocalNfo
        && !string.IsNullOrWhiteSpace(ReleaseFolderPath);

    protected override async Task OnParametersSetAsync()
    {
        if (
            loadedReleaseId != ReleaseId
            || !string.Equals(loadedReleaseFolderPath, ReleaseFolderPath, StringComparison.Ordinal)
        )
        {
            await LoadOverviewAsync();
        }
    }

    private async Task LoadOverviewAsync()
    {
        isLoading = true;

        try
        {
            await operationRunner.RunAsync<IReleaseReadRepository>(async repository =>
            {
                releaseNfo = null;
                nfoContent = null;
                hasLocalNfo = false;

                overviewUploads = await repository.GetReleaseOverviewAsync(ReleaseId);
                overviewImageUploads = await repository.GetReleaseOverviewImageUploadsAsync(
                    ReleaseId
                );
                summary = ReleaseOverviewSummaryCalculator.Calculate(overviewUploads);
                containerUrlsByLinkCrypter =
                    ReleaseOverviewSummaryCalculator.GroupCreatedContainerUrlsByLinkCrypter(
                        overviewUploads
                    );
                releaseNfo = await repository.GetReleaseNfoAsync(ReleaseId);
                nfoContent = releaseNfo?.Content;
                hasLocalNfo = ReleaseNfoService.HasLocalNfo(ReleaseFolderPath);
                loadedReleaseId = ReleaseId;
                loadedReleaseFolderPath = ReleaseFolderPath;
            });
        }
        finally
        {
            isLoading = false;
        }
    }

    public async Task ReloadAsync()
    {
        await LoadOverviewAsync();
        StateHasChanged();
    }

    private async Task RefreshAsync()
    {
        await LoadOverviewAsync();
        await OnRefreshed.InvokeAsync();
    }

    private async Task SaveNfoFileAsync()
    {
        if (releaseNfo is null)
        {
            return;
        }

        try
        {
            var result = await ReleaseNfoService.SaveNfoFileAsync(
                ReleaseFolderPath,
                releaseNfo.FileName,
                ReleaseName,
                releaseNfo.Content
            );

            switch (result)
            {
                case ReleaseNfoFileSaveResult.Saved:
                    hasLocalNfo = true;
                    toastService.Success(L["NfoFileSaved", releaseNfo.FileName]);
                    break;
                case ReleaseNfoFileSaveResult.AlreadyExists:
                    hasLocalNfo = true;
                    toastService.Error(L["NfoFileAlreadyExists"]);
                    break;
                case ReleaseNfoFileSaveResult.ReleaseFolderMissing:
                    toastService.Error(L["ReleaseFolderMissing"]);
                    break;
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            toastService.Error(L["NfoFileSaveFailed", exception.Message]);
        }
    }

    private async Task CopyTextAsync(string text)
    {
        try
        {
            var result = await jsRuntime.InvokeAsync<bool?>("bearcat.takeCopyResult");
            var copied = result ?? await jsRuntime.InvokeAsync<bool>("bearcat.copyText", text);

            if (copied)
            {
                toastService.Success(L["Copied"]);
            }
            else
            {
                toastService.Error(L["CopyFailed"]);
            }
        }
        catch (JSException)
        {
            toastService.Error(L["CopyFailed"]);
        }
    }

    private async Task ShowLinksDialogAsync(ReleaseOverviewUploadReadModel upload)
    {
        if (upload.UploadId is null)
        {
            return;
        }

        var parameters = new Dictionary<string, object?>
        {
            [nameof(UploadLinksDialog.ReleaseId)] = ReleaseId,
            [nameof(UploadLinksDialog.UploadId)] = upload.UploadId.Value,
            [nameof(UploadLinksDialog.UploadConfigName)] = upload.UploadConfigName,
        };

        await dialogService.OpenAsync<UploadLinksDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["UploadLinksTitle", upload.UploadId.Value],
                Description = L["UploadLinksDialogDescription", upload.UploadConfigName],
                Size = DialogSize.Full,
                ShowClose = true,
            }
        );
    }

    private static BadgeVariant GetContainerVariant(LinkCrypterContainerState state) =>
        state switch
        {
            LinkCrypterContainerState.Created => BadgeVariant.Default,
            LinkCrypterContainerState.CreationFailed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };

    private void ToggleDetails(int uploadConfigId)
    {
        if (!expandedUploadConfigIds.Remove(uploadConfigId))
        {
            expandedUploadConfigIds.Add(uploadConfigId);
        }
    }

    private void NavigateToUploadsView(int uploadConfigId, string view) =>
        navigationManager.NavigateTo(
            navigationManager.GetUriWithQueryParameters(
                new Dictionary<string, object?>
                {
                    ["tab"] = ReleaseDetailTab.Uploads,
                    ["view"] = view,
                    ["uploadConfigId"] = uploadConfigId,
                }
            )
        );

    private string HumanizeTimestamp(DateTime value) => timeProvider.Humanize(value);

    private static string FormatTimestamp(DateTime value) => value.ToString("g");

    private static string JoinLines(IReadOnlyList<string> lines) =>
        string.Join(Environment.NewLine, lines);

    private static string GetMonogram(string name) => name.Length <= 2 ? name : name[..2];

    private static bool IsHosterNameShown(ReleaseOverviewUploadReadModel upload) =>
        !string.Equals(
            upload.UploadConfigName,
            upload.HosterRegistrationName,
            StringComparison.OrdinalIgnoreCase
        );

    private static bool IsImageHosterNameShown(ReleaseOverviewImageUploadReadModel imageUpload) =>
        !string.Equals(
            imageUpload.ImageUploadConfigName,
            imageUpload.ImageHosterRegistrationName,
            StringComparison.OrdinalIgnoreCase
        );

    private static string GetOnlineSegmentClass(OnlineState? onlineState) =>
        onlineState switch
        {
            OnlineState.Online => "bearcat-overview-online-segment-online",
            OnlineState.Offline => "bearcat-overview-online-segment-offline",
            OnlineState.PartiallyOnline => "bearcat-overview-online-segment-partially-online",
            _ => "bearcat-overview-online-segment-unknown",
        };

    private static string? GetThumbnailUrl(ReleaseOverviewImageUploadReadModel imageUpload) =>
        (
            imageUpload.ImageUrls.FirstOrDefault(url => url.ImageSize == ImageSize.Thumbnail)
            ?? imageUpload.ImageUrls.FirstOrDefault()
        )?.Url;
}
