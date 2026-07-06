using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class UploadContainerLinksDialog(
    IServiceScopeFactory serviceScopeFactory,
    DialogService dialogService
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

    private IReadOnlyList<ReleaseUploadContainerLinkReadModel> containers = [];
    private bool isInitialized;
    private bool isLoading;

    private IReadOnlyList<string> ContainerLinks =>
        containers
            .Select(container => container.ContainerUrl)
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .ToList();

    private string CopyTextAreaId => $"container-links-copy-{UploadId}";
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
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var readRepository = scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();
            containers = await readRepository.GetUploadContainerLinksAsync(ReleaseId, UploadId);
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

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<LinkCrypterContainerService>();
        await service.DeleteFailedContainerAsync(container.Id, CancellationToken.None);
        await LoadContainersAsync();
    }

    private static BadgeVariant GetContainerVariant(LinkCrypterContainerState state) =>
        state switch
        {
            LinkCrypterContainerState.Created => BadgeVariant.Default,
            LinkCrypterContainerState.CreationFailed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };

    private async Task CloseAsync()
    {
        await DialogRef.CancelAsync();
    }
}
