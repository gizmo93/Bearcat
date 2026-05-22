using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseOverview(
    IReleaseReadRepository readRepository,
    DialogService dialogService
) : ComponentBase, IReloadableComponent
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    [Parameter]
    [EditorRequired]
    public string ReleaseFolderPath { get; set; } = null!;

    private IReadOnlyList<ReleaseOverviewUploadReadModel> overviewUploads = [];
    private string? nfoContent;
    private bool isLoading;
    private int? loadedReleaseId;
    private string? loadedReleaseFolderPath;
    private string NfoCopyTargetId => $"release-overview-nfo-{ReleaseId}";
    private bool CanCopyNfo => !isLoading && !string.IsNullOrEmpty(nfoContent);
    private string NfoCopyButtonTitle =>
        CanCopyNfo ? L["CopyNfoIntoClipboard"] : L["NoNfoFileAvailable"];

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
            nfoContent = null;

            var overviewTask = readRepository.GetReleaseOverviewAsync(ReleaseId);
            var nfoTask = GetNfoContentAsync(ReleaseFolderPath);

            await Task.WhenAll(overviewTask, nfoTask);

            overviewUploads = await overviewTask;
            nfoContent = await nfoTask;
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

    private static async Task<string?> GetNfoContentAsync(string? releaseFolderPath)
    {
        if (string.IsNullOrWhiteSpace(releaseFolderPath))
        {
            return null;
        }

        try
        {
            var nfoPath = await Task.Run(() => FindNfoPath(releaseFolderPath));
            if (string.IsNullOrWhiteSpace(nfoPath))
            {
                return null;
            }

            return await File.ReadAllTextAsync(nfoPath);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or NotSupportedException
                        or ArgumentException
            )
        {
            return null;
        }
    }

    private static string? FindNfoPath(string releaseFolderPath)
    {
        if (!Directory.Exists(releaseFolderPath))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(releaseFolderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(filePath =>
                string.Equals(
                    Path.GetExtension(filePath),
                    ".nfo",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
