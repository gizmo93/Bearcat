using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class UploadContainerLinksDialog(IReleaseReadRepository readRepository)
    : ComponentBase
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
        isLoading = true;

        try
        {
            containers = await readRepository.GetUploadContainerLinksAsync(ReleaseId, UploadId);
        }
        finally
        {
            isLoading = false;
            isInitialized = true;
        }
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
