using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class AllReleasesPage(
    IReleaseReadRepository readRepository,
    IDialogService dialogService,
    NavigationManager navigationManager)
{
    private IReadOnlyList<ReleaseListDto> releases = [];
    private ReleaseService service = null!;

    private MudMenu contextMenu = null!;

    private ReleaseListDto? contextMenuRow;

    protected override async Task OnInitializedAsync()
    {
        await RefreshReleasesAsync();
        service = ScopedServices.GetRequiredService<ReleaseService>();
    }

    private async Task DeleteReleaseAsync(ReleaseListDto release)
    {
        var result = await dialogService.ShowMessageBoxAsync(
            title: $"Delete Release {release.Name}",
            message: $"Are you sure you want to delete the release {release.Name}?",
            yesText: "Delete",
            noText: "Cancel");

        if (result is true)
        {
            await service.DeleteAsync(release.ReleaseId);
            await RefreshReleasesAsync();
        }
    }

    private async Task ShowAddReleaseDialogAsync()
    {
        var dialog = await dialogService.ShowAsync<CreateOrEditReleaseDialog>("Create Release", new DialogOptions
        {
            BackdropClick = false,
            FullWidth = true,
            CloseButton = true,
        });

        await dialog.Result;
        await RefreshReleasesAsync();
    }

    private async Task OpenMenuContent(DataGridRowClickEventArgs<ReleaseListDto> args)
    {
        contextMenuRow = args.Item;
        await contextMenu.OpenMenuAsync(args.MouseEventArgs);
    }

    private async Task RefreshReleasesAsync()
    {
        releases = await readRepository.GetReleasesAsync();
    }
}

