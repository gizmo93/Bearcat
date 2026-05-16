using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Blueprint.Pages.ManageReleases;

public partial class AllReleasesPage(
    IReleaseReadRepository readRepository,
    DialogService dialogService
)
{
    private IReadOnlyList<ReleaseDto> releases = [];
    private ReleaseService service = null!;

    protected override async Task OnInitializedAsync()
    {
        await RefreshReleasesAsync();
        service = ScopedServices.GetRequiredService<ReleaseService>();
    }

    private async Task DeleteReleaseAsync(ReleaseDto release)
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

        await service.DeleteAsync(release.ReleaseId);
        await RefreshReleasesAsync();
    }

    private async Task ShowAddReleaseDialogAsync()
    {
        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseDialog>(
            new DialogOpenOptions
            {
                Title = L["CreateRelease"],
                Description = L["CreateReleaseDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await RefreshReleasesAsync();
        }
    }

    private async Task RefreshReleasesAsync()
    {
        releases = await readRepository.GetReleasesAsync();
    }
}
