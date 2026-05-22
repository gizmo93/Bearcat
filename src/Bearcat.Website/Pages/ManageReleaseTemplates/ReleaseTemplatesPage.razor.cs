using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class ReleaseTemplatesPage(
    IReleaseTemplateReadRepository readRepository,
    DialogService dialogService,
    NavigationManager navigationManager
)
{
    private IReadOnlyList<ReleaseTemplateSummaryReadModel> releaseTemplates = [];
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadReleaseTemplatesAsync();
    }

    private async Task LoadReleaseTemplatesAsync()
    {
        isLoading = true;

        try
        {
            releaseTemplates = await readRepository.GetAllAsync();
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
            [nameof(CreateOrEditReleaseTemplateDialog.FormModel)] = new ReleaseTemplateFormModel(),
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseTemplateDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["NewReleaseTemplate"],
                Description = L["ReleaseTemplateDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        var releaseTemplateId = dialog.GetData<int>();
        navigationManager.NavigateTo($"/release-templates/{releaseTemplateId}");
    }

    private async Task ShowEditDialogAsync(ReleaseTemplateSummaryReadModel releaseTemplate)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseTemplateDialog.FormModel)] = new ReleaseTemplateFormModel
            {
                ReleaseTemplateId = releaseTemplate.ReleaseTemplateId,
                Name = releaseTemplate.Name,
                ReleaseType = releaseTemplate.ReleaseType,
                ReleaseGroupId = releaseTemplate.ReleaseGroupId,
                IsEdit = true,
            },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseTemplateDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", releaseTemplate.Name],
                Description = L["ReleaseTemplateDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseTemplatesAsync();
        }
    }

    private async Task DeleteAsync(ReleaseTemplateSummaryReadModel releaseTemplate)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", releaseTemplate.Name],
            L["DeleteReleaseTemplateConfirmation", releaseTemplate.Name],
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

        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();
        await service.DeleteAsync(releaseTemplate.ReleaseTemplateId);
        await LoadReleaseTemplatesAsync();
    }
}
