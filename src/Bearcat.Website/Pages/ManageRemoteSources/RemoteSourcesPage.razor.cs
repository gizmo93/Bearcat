using Bearcat.Domain.UseCases.ManageRemoteSources;
using Bearcat.Domain.UseCases.ManageRemoteSources.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
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
        var remoteSourceAutomationCount = await operationRunner.RunAsync(
            (RemoteSourceRegistrationService service) =>
                service.GetRemoteSourceAutomationCountAsync(registration.Id)
        );

        var parameters = new Dictionary<string, object?>
        {
            [nameof(ConfirmDeletionByTypingNameDialog.NameToConfirm)] = registration.Name,
            [nameof(ConfirmDeletionByTypingNameDialog.AffectedItemsLabel)] = L[
                "RemoteSourceAutomations"
            ].Value,
            [nameof(ConfirmDeletionByTypingNameDialog.AffectedItemsCount)] =
                remoteSourceAutomationCount,
        };

        var dialog = await dialogService.OpenAsync<ConfirmDeletionByTypingNameDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["DeleteNamedItem", registration.Name],
                ShowClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        await operationRunner.RunAsync(
            (RemoteSourceRegistrationService service) => service.RemoveAsync(registration.Id)
        );
        await LoadRegistrationsAsync();
    }
}
