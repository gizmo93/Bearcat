using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;

namespace Bearcat.Frontend.Components.Pages;

public partial class Releases(IReleaseReadRepository readRepository)
{
    private IReadOnlyList<ReleaseListDto> releases = [];
    
    protected override async Task OnInitializedAsync()
    {
        releases = await readRepository.GetReleasesAsync();
    }
}

