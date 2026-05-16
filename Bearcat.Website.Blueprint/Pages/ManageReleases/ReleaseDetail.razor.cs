using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.Blueprint.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Blueprint.Pages.ManageReleases;

public partial class ReleaseDetail(NavigationManager navigationManager, DialogService dialogService)
    : OwningComponentBase
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
            return;
        }

        release = releaseDto;
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

    private async Task DeleteReleaseAsync()
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteReleaseTitle", release.Name],
            L["DeleteReleaseConfirmation", release.Name],
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

        var service = ScopedServices.GetRequiredService<ReleaseService>();
        await service.DeleteAsync(release.ReleaseId);
        navigationManager.NavigateTo("/releases");
    }
}
