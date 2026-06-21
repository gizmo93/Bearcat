using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageForumPostTemplates;
using Bearcat.Website.Pages.PostToForum;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseOverview(
    ToastService toastService,
    DialogService dialogService,
    IServiceScopeFactory serviceScopeFactory,
    IJSRuntime jsRuntime
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
    private IReadOnlyList<ReleaseOverviewImageUploadReadModel> overviewImageUploads = [];
    private ReleaseNfoReadModel? releaseNfo;
    private ReleaseInfoReadModel? releaseInfo;
    private string? coverUrl;
    private string? nfoContent;
    private bool hasLocalNfo;
    private bool isLoading;
    private int? loadedReleaseId;
    private string? loadedReleaseFolderPath;
    private bool CanCopyNfo => !isLoading && !string.IsNullOrEmpty(nfoContent);
    private bool CanSaveNfoFile => !isLoading && releaseNfo is not null && !hasLocalNfo;

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
            releaseInfo = null;
            coverUrl = null;
            nfoContent = null;
            hasLocalNfo = false;

            overviewUploads = await readRepository.GetReleaseOverviewAsync(ReleaseId);
            overviewImageUploads = await readRepository.GetReleaseOverviewImageUploadsAsync(
                ReleaseId
            );
            releaseInfo = await readRepository.GetReleaseInfoAsync(ReleaseId);
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

    private async Task CopyNfoAsync()
    {
        if (string.IsNullOrEmpty(nfoContent))
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeAsync<bool>("bearcat.copyText", nfoContent);
            toastService.Success(L["Copied"]);
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

    private async Task ShowEditReleaseInfoDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(EditReleaseInfoDialog.ReleaseId)] = ReleaseId,
            [nameof(EditReleaseInfoDialog.ReleaseName)] = ReleaseName,
            [nameof(EditReleaseInfoDialog.ReleaseInfo)] = releaseInfo,
        };

        var dialog = await dialogService.OpenAsync<EditReleaseInfoDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = releaseInfo is null ? L["AddReleaseInfo"] : L["EditReleaseInfo"],
                Description = L["EditReleaseInfoDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        toastService.Success(L["ReleaseInfoUpdated"]);
        await LoadOverviewAsync();
        StateHasChanged();
    }

    private async Task ShowPostToForumDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(PostToForumDialog.EntityId)] = ReleaseId,
            [nameof(PostToForumDialog.EntityName)] = ReleaseName,
            [nameof(PostToForumDialog.TemplateType)] = ForumPostTemplateType.Release,
        };

        await dialogService.OpenAsync<PostToForumDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["PostNamedReleaseToForum", ReleaseName],
                Description = L["PostToForumDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );
    }

    private async Task RenderForumPostAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(RenderForumPostDialog.EntityId)] = ReleaseId,
            [nameof(RenderForumPostDialog.Type)] = ForumPostTemplateType.Release,
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

    private static string GetImageUrlsCopyTargetId(
        ReleaseOverviewImageUploadReadModel imageUpload
    ) =>
        $"release-overview-image-urls-{imageUpload.ImageUploadConfigId}-{imageUpload.ImageUploadId}";

    private static string GetImageUrlCopyTargetId(
        ReleaseOverviewImageUploadReadModel imageUpload,
        ReleaseOverviewImageUploadUrlReadModel imageUrl
    ) =>
        $"release-overview-image-url-{imageUpload.ImageUploadConfigId}-{imageUpload.ImageUploadId}-{imageUrl.ImageSize}";

    private static string GetImageUrlsText(ReleaseOverviewImageUploadReadModel imageUpload) =>
        string.Join(Environment.NewLine, imageUpload.ImageUrls.Select(url => url.Url));

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
