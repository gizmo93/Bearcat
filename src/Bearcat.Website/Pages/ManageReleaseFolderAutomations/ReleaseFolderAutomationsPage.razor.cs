using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseFolderAutomations;

public partial class ReleaseFolderAutomationsPage(
    IReleaseFolderAutomationReadRepository readRepository,
    DialogService dialogService
)
{
    private IReadOnlyList<ReleaseFolderAutomationReadModel> automations = [];
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadAutomationsAsync();
    }

    private async Task LoadAutomationsAsync()
    {
        isLoading = true;

        try
        {
            automations = await readRepository.GetAllAsync();
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseFolderAutomationDialog.FormModel)] =
                new ReleaseFolderAutomationFormModel(),
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseFolderAutomationDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["NewReleaseFolderAutomation"],
                Description = L["ReleaseFolderAutomationDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadAutomationsAsync();
        }
    }

    private async Task ShowEditDialogAsync(ReleaseFolderAutomationReadModel automation)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseFolderAutomationDialog.FormModel)] =
                new ReleaseFolderAutomationFormModel
                {
                    ReleaseFolderAutomationId = automation.ReleaseFolderAutomationId,
                    BasePath = automation.BasePath,
                    FolderNamePattern = automation.FolderNamePattern,
                    PrimaryLanguageCode = automation.PrimaryLanguageCode ?? string.Empty,
                    ReleaseTemplateId = automation.ReleaseTemplateId,
                    IsEnabled = automation.IsEnabled,
                    IsEdit = true,
                },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseFolderAutomationDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditReleaseFolderAutomation"],
                Description = L["ReleaseFolderAutomationDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadAutomationsAsync();
        }
    }

    private async Task ToggleEnabledAsync(ReleaseFolderAutomationReadModel automation)
    {
        var service = ScopedServices.GetRequiredService<ReleaseFolderAutomationService>();
        await service.SetEnabledAsync(automation.ReleaseFolderAutomationId, !automation.IsEnabled);
        await LoadAutomationsAsync();
    }

    private static string GetLanguageDisplayName(string languageCode)
    {
        var culture = CultureInfo.GetCultureInfo(languageCode);
        return $"{culture.NativeName} ({culture.TwoLetterISOLanguageName})";
    }

    private async Task DeleteAsync(ReleaseFolderAutomationReadModel automation)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteReleaseFolderAutomation"],
            L["DeleteReleaseFolderAutomationConfirmation", automation.BasePath],
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

        var service = ScopedServices.GetRequiredService<ReleaseFolderAutomationService>();
        await service.DeleteAsync(automation.ReleaseFolderAutomationId);
        await LoadAutomationsAsync();
    }
}
