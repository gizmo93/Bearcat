using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageDistributionSites;

public partial class AllDistributionSitesPage(
    DialogService dialogService,
    ToastService toastService,
    NavigationManager navigationManager,
    IScopedOperationRunner operationRunner
)
{
    private IReadOnlyList<DistributionSiteRegistrationReadModel> distributionSites = [];
    private readonly HashSet<int> loginsInProgress = [];

    private bool IsLoggingIn(DistributionSiteRegistrationReadModel distributionSite)
    {
        return loginsInProgress.Contains(distributionSite.DistributionSiteRegistrationId);
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadDistributionSitesAsync();
    }

    private async Task LoadDistributionSitesAsync()
    {
        distributionSites = await operationRunner.RunAsync(
            (IDistributionSiteRegistrationReadRepository repository) => repository.GetAllAsync()
        );
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
            [nameof(CreateOrEditDialog.Registration)] = distributionSite,
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

    private void NavigateToPostingRules(DistributionSiteRegistrationReadModel distributionSite)
    {
        navigationManager.NavigateTo(
            $"/distribution-site-registrations/{distributionSite.DistributionSiteRegistrationId}/posting-rules"
        );
    }

    private async Task ToggleIsActiveAsync(DistributionSiteRegistrationReadModel distributionSite)
    {
        await operationRunner.RunAsync(
            (DistributionSiteRegistrationService service) =>
                service.ToggleIsActiveAsync(distributionSite.DistributionSiteRegistrationId)
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
        var forumPostingRuleCount = await operationRunner.RunAsync(
            (DistributionSiteRegistrationService service) =>
                service.GetForumPostingRuleCountAsync(
                    distributionSite.DistributionSiteRegistrationId
                )
        );

        var parameters = new Dictionary<string, object?>
        {
            [nameof(ConfirmDeletionByTypingNameDialog.NameToConfirm)] = distributionSite.Name,
            [nameof(ConfirmDeletionByTypingNameDialog.AffectedItemsLabel)] = L[
                "ForumPostingRules"
            ].Value,
            [nameof(ConfirmDeletionByTypingNameDialog.AffectedItemsCount)] = forumPostingRuleCount,
        };

        var dialog = await dialogService.OpenAsync<ConfirmDeletionByTypingNameDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["DeleteNamedItem", distributionSite.Name],
                ShowClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        await operationRunner.RunAsync(
            (DistributionSiteRegistrationService service) =>
                service.DeleteAsync(distributionSite.DistributionSiteRegistrationId)
        );
        await LoadDistributionSitesAsync();
    }

    private async Task TestLoginAsync(DistributionSiteRegistrationReadModel distributionSite)
    {
        loginsInProgress.Add(distributionSite.DistributionSiteRegistrationId);

        try
        {
            var result = await operationRunner.RunAsync(
                (DistributionSiteSessionService service) =>
                    service.TestLoginAsync(distributionSite.DistributionSiteRegistrationId)
            );

            if (result.IsSuccess)
            {
                toastService.Success(L["LoginSuccessful", distributionSite.Name]);
            }
            else
            {
                toastService.Error(
                    L["LoginFailed", distributionSite.Name, result.ErrorMessage ?? string.Empty]
                );
            }
        }
        finally
        {
            loginsInProgress.Remove(distributionSite.DistributionSiteRegistrationId);
        }

        await LoadDistributionSitesAsync();
    }
}
