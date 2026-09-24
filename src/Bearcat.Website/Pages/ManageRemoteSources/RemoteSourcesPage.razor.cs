using Bearcat.Domain.UseCases.ManageRemoteSources;
using Bearcat.Domain.UseCases.ManageRemoteSources.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageRemoteSources;

public partial class RemoteSourcesPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private readonly HashSet<int> testingRegistrationIds = [];
    private IReadOnlyList<RemoteSourceRegistrationReadModel> registrations = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadRegistrationsAsync();
    }

    private async Task LoadRegistrationsAsync()
    {
        registrations = await operationRunner.RunAsync(
            (IRemoteSourceRegistrationReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task ShowAddDialogAsync()
    {
        await OpenDialogAsync(registration: null, L["AddRemoteSource"]);
    }

    private async Task ShowEditDialogAsync(RemoteSourceRegistrationReadModel registration)
    {
        await OpenDialogAsync(registration, L["EditNamedItem", registration.Name]);
    }

    private async Task OpenDialogAsync(
        RemoteSourceRegistrationReadModel? registration,
        string title
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AddOrEditRemoteSource.Registration)] = registration,
        };

        var dialog = await dialogService.OpenAsync<AddOrEditRemoteSource>(
            parameters,
            new DialogOpenOptions
            {
                Title = title,
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadRegistrationsAsync();
        }
    }

    private async Task TestConnectionAsync(RemoteSourceRegistrationReadModel registration)
    {
        testingRegistrationIds.Add(registration.Id);

        try
        {
            var result = await operationRunner.RunAsync(
                (RemoteSourceRegistrationService service) =>
                    service.TestConnectionAsync(registration.Id)
            );

            if (result.IsSuccess)
            {
                toastService.Success(
                    L["RemoteSourceConnectionSuccessful", registration.Name, result.RootFolderCount]
                );
                return;
            }

            toastService.Error(
                L[
                    "RemoteSourceConnectionFailed",
                    registration.Name,
                    result.ErrorMessage ?? string.Empty
                ]
            );
        }
        finally
        {
            testingRegistrationIds.Remove(registration.Id);
        }
    }

    private async Task ToggleIsActiveAsync(RemoteSourceRegistrationReadModel registration)
    {
        await operationRunner.RunAsync(
            (RemoteSourceRegistrationService service) =>
                service.ToggleIsActiveAsync(registration.Id)
        );

        toastService.Success(
            registration.IsActive
                ? L["RemoteSourceDeactivated", registration.Name]
                : L["RemoteSourceActivated", registration.Name]
        );
        await LoadRegistrationsAsync();
    }

    private async Task DeleteAsync(RemoteSourceRegistrationReadModel registration)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", registration.Name],
            L["DeleteRemoteSourceConfirmation", registration.Name],
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

        await operationRunner.RunAsync(
            (RemoteSourceRegistrationService service) => service.RemoveAsync(registration.Id)
        );
        await LoadRegistrationsAsync();
    }
}
