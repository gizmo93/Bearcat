using Bearcat.Domain.UseCases.ManageLinkCrypters;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Dto;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Blueprint.Pages.ManageLinkCrypters;

public partial class AllLinkCryptersPage(
    ILinkCrypterRegistrationReadRepository readRepository,
    DialogService dialogService,
    ToastService toastService
)
{
    private IReadOnlyList<LinkCrypterRegistrationDto> crypters = [];
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
                Title = "Add crypter",
                Description = "Store crypter configuration and verify connectivity when needed.",
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

    private async Task ShowEditDialogAsync(LinkCrypterRegistrationDto crypter)
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
                Title = $"Edit {crypter.Name}",
                Description = "Store crypter configuration and verify connectivity when needed.",
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

    private async Task ToggleIsActiveAsync(LinkCrypterRegistrationDto crypter)
    {
        await linkCrypterService.ToggleIsActiveAsync(crypter.LinkCrypterRegistrationId);

        var status = crypter.IsActive ? "deactivated" : "activated";
        toastService.Success($"Link crypter registration {crypter.Name} {status}");
        await LoadCryptersAsync();
    }

    private async Task DeleteAsync(LinkCrypterRegistrationDto crypter)
    {
        var result = await dialogService.ConfirmAsync(
            $"Delete {crypter.Name}",
            $"Are you sure you want to delete link crypter registration {crypter.Name}?",
            new ConfirmDialogOptions
            {
                ConfirmText = "Delete",
                CancelText = "Cancel",
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

    private async Task TryLoginAsync(LinkCrypterRegistrationDto crypter)
    {
        var service = ScopedServices.GetRequiredService<LinkCrypterService>();
        var result = await service.TryLoginAsync(crypter.LinkCrypterRegistrationId);

        if (result.IsSuccess)
        {
            toastService.Success($"Login for registration {crypter.Name} successful");
            return;
        }

        toastService.Error($"Login for registration {crypter.Name} failed: {result.ErrorMessage}");
    }
}
