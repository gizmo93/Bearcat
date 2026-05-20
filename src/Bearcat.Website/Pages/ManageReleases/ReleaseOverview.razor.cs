using Bearcat.Domain.UseCases.ManageReleases.Dto;
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

    private IReadOnlyList<ReleaseOverviewUploadDto> overviewUploads = [];
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadOverviewAsync();
    }

    private async Task LoadOverviewAsync()
    {
        isLoading = true;

        try
        {
            overviewUploads = await readRepository.GetReleaseOverviewAsync(ReleaseId);
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

    private async Task ShowLinksDialogAsync(ReleaseOverviewUploadDto upload)
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
        ReleaseOverviewUploadDto upload,
        ReleaseOverviewLinkCrypterLinkDto link
    ) => $"release-overview-container-{upload.UploadId}-{link.LinkCrypterContainerId}";

    private static string GetPasswordCopyTargetId(ReleaseOverviewUploadDto upload) =>
        $"release-overview-password-{upload.UploadId}";
}
