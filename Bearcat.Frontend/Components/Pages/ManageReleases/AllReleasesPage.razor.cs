using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class AllReleasesPage(
    IReleaseReadRepository readRepository,
    NavigationManager navigationManager)
{
    private IReadOnlyList<ReleaseListDto> releases = [];
    
    protected override async Task OnInitializedAsync()
    {
        releases = await readRepository.GetReleasesAsync();
    }
}

