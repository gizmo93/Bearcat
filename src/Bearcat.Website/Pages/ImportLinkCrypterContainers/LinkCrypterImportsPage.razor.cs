using Bearcat.Domain.UseCases.ImportLinkCrypterContainers;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ImportLinkCrypterContainers;

public partial class LinkCrypterImportsPage(
    DialogService dialogService,
    ToastService toastService,
    NavigationManager navigationManager,
    IScopedOperationRunner operationRunner,
    TimeProvider timeProvider
) : ComponentBase
{
    private const string ImportGridClass =
        "lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)_minmax(0,0.8fr)_minmax(0,1fr)_9rem]";

    private IReadOnlyList<LinkCrypterImportReadModel> imports = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadImportsAsync();
    }

    private async Task LoadImportsAsync()
    {
        imports = await operationRunner.RunAsync(
            (ILinkCrypterImportReadRepository repository) => repository.GetAllAsync()
        );
    }

    private static bool HasBackgroundWork(LinkCrypterImportState state)
    {
        return state
            is LinkCrypterImportState.ReadingContainers
                or LinkCrypterImportState.CheckingFiles
                or LinkCrypterImportState.Importing;
    }

    private void OpenImport(LinkCrypterImportReadModel import)
    {
        navigationManager.NavigateTo($"/link-crypter-imports/{import.Id}");
    }

    private async Task ShowNewImportDialogAsync()
    {
        var dialog = await dialogService.OpenAsync<NewLinkCrypterImportDialog>(
            new DialogOpenOptions
            {
                Title = L["NewLinkCrypterImport"],
                Description = L["NewLinkCrypterImportDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        navigationManager.NavigateTo($"/link-crypter-imports/{dialog.GetData<int>()}");
    }

    private async Task DeleteAsync(LinkCrypterImportReadModel import)
    {
        var confirmation = await dialogService.ConfirmAsync(
            L["DeleteLinkCrypterImport"],
            L["DeleteLinkCrypterImportConfirmation", import.LinkCrypterRegistrationName],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!confirmation.Confirmed)
        {
            return;
        }

        try
        {
            await operationRunner.RunAsync(
                (LinkCrypterImportService service) => service.DeleteAsync(import.Id)
            );
            toastService.Success(L["LinkCrypterImportDeleted"]);
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            toastService.Error(exception.Message);
        }

        await LoadImportsAsync();
    }
}
