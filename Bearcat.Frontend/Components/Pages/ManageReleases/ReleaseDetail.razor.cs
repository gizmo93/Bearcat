using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Frontend.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class ReleaseDetail(NavigationManager navigationManager)
{
    [Parameter]
    public int ReleaseId { get; set; }
    
    private IReleaseReadRepository releaseReadRepository = null!;

    private ReleaseDto release = null!;

    private bool isInitialized;
    
    private readonly Dictionary<string, IReloadableComponent> reloadableComponents = new();

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
        isInitialized = true;
    }

    private async Task HandleChangeAffectingOtherComponentsAsync(string componentName)
    {
        var affectedComponents = reloadableComponents
            .Where(c => c.Key != componentName)
            .Select(c => c.Value);

        foreach (var component in affectedComponents)
        {
            await component.ReloadAsync();
        }
    }
}

