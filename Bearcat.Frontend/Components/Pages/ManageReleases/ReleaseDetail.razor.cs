using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class ReleaseDetail(NavigationManager navigationManager)
{
    [Parameter]
    public int ReleaseId { get; set; }
    
    private IReleaseReadRepository releaseReadRepository = null!;
    
    private ReleaseDto release = null!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        releaseReadRepository = ScopedServices.GetRequiredService<IReleaseReadRepository>();

        var releaseDto = await releaseReadRepository.GetReleaseAsync(ReleaseId);
        
        if (releaseDto is null)
        {
            navigationManager.NotFound();
        }

        release = releaseDto!;
    }
}

