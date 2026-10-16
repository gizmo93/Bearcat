using Bearcat.Domain.UseCases.ManageRemoteSources;
using Bearcat.Domain.UseCases.ManageRemoteSources.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;
using Bearcat.Website.Pages.ManageRemoteSourceAutomations;
using Bearcat.Website.Pages.ManageRemoteSources;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
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
    public EventCallback<FirstStartSetupCreatedSource> OnSourceCreated { get; set; }

    private IReadOnlyList<RemoteSourceRegistrationReadModel> registrations = [];
    private List<SelectOption<int?>> registrationOptions = [];
    private int? selectedRegistrationId;
    private RemoteSourceConnectionTestResult? connectionTestResult;
    private bool isTestingConnection;
    private bool isLoading = true;

    private string SelectedRegistrationName =>
        registrations.Single(registration => registration.Id == selectedRegistrationId).Name;

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistrationsAsync();
        selectedRegistrationId = registrations.FirstOrDefault()?.Id;
        isLoading = false;
    }

    private async Task LoadRegistrationsAsync()
    {
        var allRegistrations = await operationRunner.RunAsync(
            (IRemoteSourceRegistrationReadRepository repository) => repository.GetAllAsync()
        );
        registrations = allRegistrations.Where(registration => registration.IsActive).ToList();
        registrationOptions = registrations
            .Select(registration => new SelectOption<int?>(registration.Id, registration.Name))
            .ToList();
    }

    private void SelectRegistration(int? registrationId)
    {
        selectedRegistrationId = registrationId;
        connectionTestResult = null;
    }

    private async Task TestConnectionAsync()
    {
        var registrationId = selectedRegistrationId!.Value;
        isTestingConnection = true;
        connectionTestResult = null;
        StateHasChanged();

        try
        {
            connectionTestResult = await operationRunner.RunAsync(
                (RemoteSourceRegistrationService service) =>
                    service.TestConnectionAsync(registrationId)
            );
        }
        finally
        {
            isTestingConnection = false;
        }
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

        if (dialog.Cancelled)
        {
            return;
        }

        await LoadRegistrationsAsync();
        SelectRegistration(dialog.GetData<int>());
        await TestConnectionAsync();
    }

    private async Task ShowCreateRemoteSourceAutomationDialogAsync()
    {
        var formModel = new RemoteSourceAutomationFormModel
        {
            Name = SelectedRegistrationName,
            RemoteSourceRegistrationId = selectedRegistrationId,
            ReleaseTemplateId = ReleaseTemplateId,
            IgnoreExistingOnFirstScan = true,
        };
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditRemoteSourceAutomationDialog.FormModel)] = formModel,
            [
                nameof(
                    CreateOrEditRemoteSourceAutomationDialog.RejectArchiveOutputFolderInsideTargetPath
                )
            ] = true,
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

        await OnSourceCreated.InvokeAsync(
            new FirstStartSetupCreatedSource(
                FirstStartSetupSourceKind.RemoteSource,
                RemoteSourceAutomationName: formModel.Name.Trim(),
                FolderPath: formModel.TargetPath.Trim(),
                TestReleaseId: null,
                TestReleaseInitialUploadCreationResult: null
            )
        );
    }
}
