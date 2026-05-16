using Bearcat.Domain.UseCases.ManageLinkCrypters;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Dto;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageLinkCrypters;

public partial class AllLinkCryptersPage(
    ILinkCrypterRegistrationReadRepository readRepository,
    IDialogService dialogService,
    ISnackbar snackbar
)
{
    private IReadOnlyList<LinkCrypterRegistrationDto> crypters = [];
    private LinkCrypterService linkCrypterService = null!;

    private MudMenu contextMenu = null!;

    private LinkCrypterRegistrationDto? contextMenuRow;

    protected override async Task OnInitializedAsync()
    {
        await LoadCryptersAsync();
        linkCrypterService = ScopedServices.GetRequiredService<LinkCrypterService>();
    }

    private async Task LoadCryptersAsync()
    {
        crypters = await readRepository.GetAllAsync();
    }

    private async Task OpenMenuContent(DataGridRowClickEventArgs<LinkCrypterRegistrationDto> args)
    {
        contextMenuRow = args.Item;
        await contextMenu.OpenMenuAsync(args.MouseEventArgs);
    }

    private async Task ShowAddDialogAsync()
    {
        var dialog = await dialogService.ShowAsync<CreateOrEditDialog>(
            title: "Add Crypter",
            options: new DialogOptions { BackdropClick = false, FullWidth = true }
        );

        await dialog.Result;
        await LoadCryptersAsync();
    }

    private async Task ShowEditDialogAsync(LinkCrypterRegistrationDto crypter)
    {
        var parameters = new DialogParameters<CreateOrEditDialog>
        {
            { x => x.LinkCrypterRegistrationId, crypter.LinkCrypterRegistrationId },
        };

        var dialog = await dialogService.ShowAsync<CreateOrEditDialog>(
            title: $"Edit {crypter.Name}",
            parameters: parameters,
            options: new DialogOptions { BackdropClick = false, FullWidth = true }
        );

        await dialog.Result;
        await LoadCryptersAsync();
    }

    private async Task ToggleIsActiveAsync(LinkCrypterRegistrationDto crypter)
    {
        await linkCrypterService.ToggleIsActiveAsync(crypter.LinkCrypterRegistrationId);

        var status = crypter.IsActive ? "deactivated" : "activated";
        snackbar.Add($"Link crypter registration {crypter.Name} {status}", Severity.Success);
        await LoadCryptersAsync();
    }

    private async Task DeleteAsync(LinkCrypterRegistrationDto crypter)
    {
        var message = $"Are you sure you want to delete link crypter registration {crypter.Name}?";

        var result = await dialogService.ShowMessageBoxAsync(
            title: $"Delete {crypter.Name}",
            message: message,
            yesText: "Delete",
            noText: "Cancel"
        );

        if (result == true)
        {
            await linkCrypterService.DeleteAsync(crypter.LinkCrypterRegistrationId);
        }

        await LoadCryptersAsync();
    }

    private async Task TryLoginAsync(LinkCrypterRegistrationDto crypter)
    {
        var service = ScopedServices.GetRequiredService<LinkCrypterService>();
        var result = await service.TryLoginAsync(crypter.LinkCrypterRegistrationId);

        if (result.IsSuccess)
        {
            snackbar.Add($"Login for registration {crypter.Name} successful", Severity.Success);
        }
        else
        {
            snackbar.Add(
                $"Login for registration {crypter.Name} failed: {result.ErrorMessage}",
                Severity.Error
            );
        }
    }
}
