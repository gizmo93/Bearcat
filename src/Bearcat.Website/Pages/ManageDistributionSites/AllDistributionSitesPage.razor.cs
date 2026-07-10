using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageDistributionSites;

public partial class AllDistributionSitesPage(
    IDistributionSiteRegistrationReadRepository readRepository,
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<DistributionSiteRegistrationReadModel> distributionSites = [];
    private DistributionSiteRegistrationService registrationService = null!;

    protected override async Task OnInitializedAsync()
    {
        await LoadDistributionSitesAsync();
        registrationService =
            ScopedServices.GetRequiredService<DistributionSiteRegistrationService>();
    }

    private async Task LoadDistributionSitesAsync()
    {
        distributionSites = await readRepository.GetAllAsync();
    }

    private async Task ShowAddDialogAsync()
    {
        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            new DialogOpenOptions
            {
                Title = L["AddDistributionSite"],
                Description = L["DistributionSiteDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadDistributionSitesAsync();
        }
    }

    private async Task ShowEditDialogAsync(DistributionSiteRegistrationReadModel distributionSite)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditDialog.DistributionSiteRegistrationId)] =
                distributionSite.DistributionSiteRegistrationId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", distributionSite.Name],
                Description = L["DistributionSiteDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadDistributionSitesAsync();
        }
    }

    private async Task ToggleIsActiveAsync(DistributionSiteRegistrationReadModel distributionSite)
    {
        await registrationService.ToggleIsActiveAsync(
            distributionSite.DistributionSiteRegistrationId
        );

        toastService.Success(
            distributionSite.IsActive
                ? L["DistributionSiteRegistrationDeactivated", distributionSite.Name]
                : L["DistributionSiteRegistrationActivated", distributionSite.Name]
        );
        await LoadDistributionSitesAsync();
    }

    private async Task DeleteAsync(DistributionSiteRegistrationReadModel distributionSite)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", distributionSite.Name],
            L["DeleteDistributionSiteRegistrationConfirmation", distributionSite.Name],
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

        await registrationService.DeleteAsync(distributionSite.DistributionSiteRegistrationId);
        await LoadDistributionSitesAsync();
    }

    private async Task TestLoginAsync(DistributionSiteRegistrationReadModel distributionSite)
    {
        var result = await operationRunner.RunAsync(
            (DistributionSiteSessionService service) =>
                service.TestLoginAsync(distributionSite.DistributionSiteRegistrationId)
        );

        if (result.IsSuccess)
        {
            toastService.Success(L["LoginSuccessful", distributionSite.Name]);
            await LoadDistributionSitesAsync();
            return;
        }

        toastService.Error(
            L["LoginFailed", distributionSite.Name, result.ErrorMessage ?? string.Empty]
        );
        await LoadDistributionSitesAsync();
    }
}
