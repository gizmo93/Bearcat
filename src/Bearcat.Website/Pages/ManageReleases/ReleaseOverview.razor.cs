using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageForumPostTemplates;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseOverview(
    ToastService toastService,
    DialogService dialogService,
    IServiceScopeFactory serviceScopeFactory
) : ComponentBase, IReloadableComponent
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    [Parameter]
    [EditorRequired]
    public string ReleaseName { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string ReleaseFolderPath { get; set; } = null!;

    private IReadOnlyList<ReleaseOverviewUploadReadModel> overviewUploads = [];
    private ReleaseNfoReadModel? releaseNfo;
    private string? coverUrl;
    private string? nfoContent;
    private bool hasLocalNfo;
    private bool isLoading;
    private int? loadedReleaseId;
    private string? loadedReleaseFolderPath;
    private string NfoCopyTargetId => $"release-overview-nfo-{ReleaseId}";
    private bool CanCopyNfo => !isLoading && !string.IsNullOrEmpty(nfoContent);
    private bool CanSaveNfoFile => !isLoading && releaseNfo is not null && !hasLocalNfo;
    private string NfoCopyButtonTitle =>
        CanCopyNfo ? L["CopyNfoIntoClipboard"] : L["NoNfoFileAvailable"];
    private string NfoSaveButtonTitle =>
        releaseNfo is null ? L["NoNfoFileAvailable"]
        : hasLocalNfo ? L["NfoFileAlreadyExists"]
        : L["SaveNfoFile"];
    private string CoverDownloadUrl => $"/releases/{ReleaseId}/cover";
    private string CoverDownloadFileName => GetCoverDownloadFileName();

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
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var readRepository = scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();

            releaseNfo = null;
            coverUrl = null;
            nfoContent = null;
            hasLocalNfo = false;

            overviewUploads = await readRepository.GetReleaseOverviewAsync(ReleaseId);
            var releaseInfo = await readRepository.GetReleaseInfoAsync(ReleaseId);
            coverUrl = releaseInfo?.CoverUrl;
            releaseNfo = await readRepository.GetReleaseNfoAsync(ReleaseId);
            nfoContent = releaseNfo?.Content;
            hasLocalNfo = ReleaseNfoService.HasLocalNfo(ReleaseFolderPath);
            loadedReleaseId = ReleaseId;
            loadedReleaseFolderPath = ReleaseFolderPath;
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

    private async Task RenderForumPostAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(RenderForumPostDialog.ReleaseId)] = ReleaseId,
        };

        await dialogService.OpenAsync<RenderForumPostDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["RenderForumPostForRelease", ReleaseName],
                Description = L["RenderForumPostDescription"],
                Size = DialogSize.Full,
                ShowClose = true,
            }
        );
    }

    private static BadgeVariant GetUploadVariant(UploadState? state) =>
        state switch
        {
            UploadState.Uploading => BadgeVariant.Default,
            UploadState.CancellationRequested => BadgeVariant.Secondary,
            UploadState.Pending => BadgeVariant.Secondary,
            UploadState.Failed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };

    private static BadgeVariant GetContainerVariant(LinkCrypterContainerState state) =>
        state switch
        {
            LinkCrypterContainerState.Created => BadgeVariant.Default,
            LinkCrypterContainerState.CreationFailed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };

    private static string GetContainerCopyTargetId(
        ReleaseOverviewUploadReadModel upload,
        ReleaseOverviewLinkCrypterLinkReadModel link
    ) => $"release-overview-container-{upload.UploadId}-{link.LinkCrypterContainerId}";

    private static string GetPasswordCopyTargetId(ReleaseOverviewUploadReadModel upload) =>
        $"release-overview-password-{upload.UploadId}";

    private string GetCoverDownloadFileName()
    {
        if (Uri.TryCreate(coverUrl, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return $"{SanitizeFileName(ReleaseName)}-cover.jpg";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(
            value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()
        );

        return string.IsNullOrWhiteSpace(sanitized) ? "release" : sanitized;
    }
}
