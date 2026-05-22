using Bearcat.Domain.UseCases.ManageLinkCrypters;
using Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageLinkCrypters;

public partial class AllLinkCryptersPage(
    ILinkCrypterRegistrationReadRepository readRepository,
    DialogService dialogService,
    ToastService toastService
)
{
    private IReadOnlyList<LinkCrypterRegistrationReadModel> crypters = [];
    private LinkCrypterService linkCrypterService = null!;

    protected override async Task OnInitializedAsync()
    {
        await LoadCryptersAsync();
        linkCrypterService = ScopedServices.GetRequiredService<LinkCrypterService>();
    }

    private async Task LoadCryptersAsync()
    {
        crypters = await readRepository.GetAllAsync();
    }

    private async Task ShowAddDialogAsync()
    {
        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            new DialogOpenOptions
            {
                Title = L["AddCrypter"],
                Description = L["CrypterDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadCryptersAsync();
        }
    }

    private async Task ShowEditDialogAsync(LinkCrypterRegistrationReadModel crypter)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditDialog.LinkCrypterRegistrationId)] =
                crypter.LinkCrypterRegistrationId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", crypter.Name],
                Description = L["CrypterDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadCryptersAsync();
        }
    }

    private async Task ToggleIsActiveAsync(LinkCrypterRegistrationReadModel crypter)
    {
        await linkCrypterService.ToggleIsActiveAsync(crypter.LinkCrypterRegistrationId);

        toastService.Success(
            crypter.IsActive
                ? L["LinkCrypterRegistrationDeactivated", crypter.Name]
                : L["LinkCrypterRegistrationActivated", crypter.Name]
        );
        await LoadCryptersAsync();
    }

    private async Task DeleteAsync(LinkCrypterRegistrationReadModel crypter)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", crypter.Name],
            L["DeleteLinkCrypterRegistrationConfirmation", crypter.Name],
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

        await linkCrypterService.DeleteAsync(crypter.LinkCrypterRegistrationId);
        await LoadCryptersAsync();
    }

    private async Task TryLoginAsync(LinkCrypterRegistrationReadModel crypter)
    {
        var service = ScopedServices.GetRequiredService<LinkCrypterService>();
        var result = await service.TryLoginAsync(crypter.LinkCrypterRegistrationId);

        if (result.IsSuccess)
        {
            toastService.Success(L["LoginSuccessful", crypter.Name]);
            return;
        }

        toastService.Error(L["LoginFailed", crypter.Name, result.ErrorMessage ?? string.Empty]);
    }
}
