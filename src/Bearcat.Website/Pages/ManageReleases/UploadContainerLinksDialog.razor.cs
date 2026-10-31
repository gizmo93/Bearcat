using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class UploadContainerLinksDialog(
    IScopedOperationRunner operationRunner,
    DialogService dialogService,
    ToastService toastService
)
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int UploadId { get; set; }

    [Parameter]
    public string UploadConfigName { get; set; } = null!;

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private readonly HashSet<int> containerIdsWithRunningAction = [];
    private IReadOnlyList<ReleaseUploadContainerLinkReadModel> containers = [];
    private bool isInitialized;
    private bool isLoading;

    private IReadOnlyList<string> ContainerLinks =>
        containers
            .Select(container => container.ContainerUrl)
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .ToList();

    private string ContainerLinksText => string.Join(Environment.NewLine, ContainerLinks);

    protected override async Task OnInitializedAsync()
    {
        await LoadContainersAsync();
        isInitialized = true;
    }

    private async Task LoadContainersAsync()
    {
        isLoading = true;

        try
        {
            containers = await operationRunner.RunAsync(
                (IReleaseReadRepository repository) =>
                    repository.GetUploadContainerLinksAsync(ReleaseId, UploadId)
            );
        }
        finally
        {
            isLoading = false;
        }
    }

    private static bool CanDelete(ReleaseUploadContainerLinkReadModel container) =>
        container
            is {
                State: LinkCrypterContainerState.CreationFailed,
                Scope: LinkCrypterContainerScope.Release,
            };

    private async Task DeleteContainerAsync(ReleaseUploadContainerLinkReadModel container)
    {
        if (!CanDelete(container))
        {
            return;
        }

        var result = await dialogService.ConfirmAsync(
            L["DeleteLinkCrypterContainer"],
            L["DeleteLinkCrypterContainerConfirmation"],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await operationRunner.RunAsync(
            (LinkCrypterContainerService service) =>
                service.DeleteFailedContainerAsync(container.Id, CancellationToken.None)
        );
        await LoadContainersAsync();
    }

    private static bool CanForceUpdate(ReleaseUploadContainerLinkReadModel container) =>
        container
            is {
                State: LinkCrypterContainerState.Created or LinkCrypterContainerState.UpdateFailed,
                Scope: LinkCrypterContainerScope.Release,
                HasActiveLinkCrypterConfig: true,
            };

    private static bool CanRecreate(ReleaseUploadContainerLinkReadModel container) =>
        container
            is {
                State: LinkCrypterContainerState.UpdateFailed,
                Scope: LinkCrypterContainerScope.Release,
                HasActiveLinkCrypterConfig: true,
            };

    private async Task ForceUpdateContainerAsync(ReleaseUploadContainerLinkReadModel container)
    {
        containerIdsWithRunningAction.Add(container.Id);

        try
        {
            var updatedContainer = await operationRunner.RunAsync(
                (LinkCrypterContainerService service) =>
                    service.ForceUpdateContainerAsync(container.Id, CancellationToken.None)
            );
            LinkCrypterContainerActionToasts.ShowForceUpdateResult(
                toastService: toastService,
                localizer: L,
                previousContainerUrl: container.ContainerUrl,
                container: updatedContainer
            );
        }
        finally
        {
            containerIdsWithRunningAction.Remove(container.Id);
        }

        await LoadContainersAsync();
    }

    private async Task RecreateContainerAsync(ReleaseUploadContainerLinkReadModel container)
    {
        var result = await dialogService.ConfirmAsync(
            L["RecreateLinkCrypterContainer"],
            L["RecreateLinkCrypterContainerConfirmation"],
            new ConfirmDialogOptions { ConfirmText = L["Recreate"], CancelText = L["Cancel"] }
        );

        if (!result.Confirmed)
        {
            return;
        }

        containerIdsWithRunningAction.Add(container.Id);
        StateHasChanged();

        try
        {
            var recreatedContainer = await operationRunner.RunAsync(
                (LinkCrypterContainerService service) =>
                    service.RecreateContainerAsync(container.Id, CancellationToken.None)
            );
            LinkCrypterContainerActionToasts.ShowRecreateResult(
                toastService: toastService,
                localizer: L,
                container: recreatedContainer
            );
        }
        finally
        {
            containerIdsWithRunningAction.Remove(container.Id);
        }

        await LoadContainersAsync();
    }

    private static BadgeVariant GetContainerVariant(LinkCrypterContainerState state) =>
        state switch
        {
            LinkCrypterContainerState.Created => BadgeVariant.Default,
            LinkCrypterContainerState.CreationFailed => BadgeVariant.Destructive,
            LinkCrypterContainerState.UpdateFailed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };

    private async Task CloseAsync()
    {
        await DialogRef.CancelAsync();
    }
}
