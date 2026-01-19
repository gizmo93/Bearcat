using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class AllReleasesPage(
    IReleaseReadRepository readRepository,
    IDialogService dialogService,
    NavigationManager navigationManager)
{
    private IReadOnlyList<ReleaseListDto> releases = [];
    
    protected override async Task OnInitializedAsync()
    {
        await RefreshReleasesAsync();
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
    
    private async Task RefreshReleasesAsync()
    {
        releases = await readRepository.GetReleasesAsync();
    }
}

