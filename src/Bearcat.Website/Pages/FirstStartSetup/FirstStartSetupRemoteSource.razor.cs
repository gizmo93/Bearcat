using Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;
using Bearcat.Website.Pages.ManageRemoteSourceAutomations;
using Bearcat.Website.Pages.ManageRemoteSources;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupRemoteSource(
    DialogService dialogService,
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public int ReleaseTemplateId { get; set; }

    [Parameter]
    public EventCallback OnBack { get; set; }

    [Parameter]
    public EventCallback OnSourceCreated { get; set; }

    private bool? hasActiveRemoteSourceRegistration;
    private bool isSourceCreated;

    protected override async Task OnInitializedAsync()
    {
        await LoadHasActiveRemoteSourceRegistrationAsync();
    }

    private async Task LoadHasActiveRemoteSourceRegistrationAsync()
    {
        var registrations = await operationRunner.RunAsync(
            (IRemoteSourceRegistrationReadRepository repository) => repository.GetAllAsync()
        );
        hasActiveRemoteSourceRegistration = registrations.Any(registration =>
            registration.IsActive
        );
    }

    private async Task ShowAddRemoteSourceDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AddOrEditRemoteSource.Registration)] = null,
        };

        var dialog = await dialogService.OpenAsync<AddOrEditRemoteSource>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddRemoteSource"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadHasActiveRemoteSourceRegistrationAsync();
        }
    }

    private async Task ShowCreateRemoteSourceAutomationDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditRemoteSourceAutomationDialog.FormModel)] =
                new RemoteSourceAutomationFormModel
                {
                    ReleaseTemplateId = ReleaseTemplateId,
                    IgnoreExistingOnFirstScan = true,
                },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditRemoteSourceAutomationDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["NewRemoteSourceAutomation"],
                Description = L["RemoteSourceAutomationDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        isSourceCreated = true;
        await OnSourceCreated.InvokeAsync();
    }
}
