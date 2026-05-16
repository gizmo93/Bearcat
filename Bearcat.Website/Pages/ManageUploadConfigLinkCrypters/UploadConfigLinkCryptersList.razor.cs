using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageUploadConfigLinkCrypters;

public partial class UploadConfigLinkCryptersList(IDialogService dialogService)
{
    [Parameter]
    [EditorRequired]
    public int UploadConfigId { get; set; }

    [Parameter]
    [EditorRequired]
    public string? ReleaseName { get; set; }

    private IUploadConfigLinkCrypterReadRepository readRepository = null!;

    private IReadOnlyList<UploadConfigLinkCrypterDto> uploadConfigLinkCrypters = [];

    private UploadConfigLinkCrypterDto? selectedItem;

    private MudMenu contextMenu = null!;

    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        readRepository =
            ScopedServices.GetRequiredService<IUploadConfigLinkCrypterReadRepository>();
        await LoadDataAsync();
        isInitialized = true;
    }

    private async Task ShowAddDialogAsync()
    {
        await ShowAddOrEditDialogAsync(null);
    }

    private async Task ShowEditDialogAsync(UploadConfigLinkCrypterDto config)
    {
        await ShowAddOrEditDialogAsync(config);
    }

    private async Task ShowAddOrEditDialogAsync(UploadConfigLinkCrypterDto? config)
    {
        var parameters = new DialogParameters<CreateOrEditUploadConfigLinkCrypter>
        {
            { dlg => dlg.UploadConfigId, UploadConfigId },
            { dlg => dlg.ReleaseName, ReleaseName },
        };

        if (config is not null)
        {
            parameters.Add(dlg => dlg.UploadConfigLinkCrypterId, config.UploadConfigLinkCrypterId);
        }

        var dialogTitle = config is null
            ? "Add Link Crypter Container"
            : "Edit Link Crypter Container";

        var dialog = await dialogService.ShowAsync<CreateOrEditUploadConfigLinkCrypter>(
            title: dialogTitle,
            parameters: parameters,
            options: new DialogOptions
            {
                BackdropClick = false,
                CloseOnEscapeKey = false,
                CloseButton = true,
                FullWidth = true,
            }
        );

        await dialog.Result;
        await LoadDataAsync();
    }

    private async Task ShowDeleteDialogAsync(UploadConfigLinkCrypterDto uploadConfigLinkCrypterDto)
    {
        var result = await dialogService.ShowMessageBoxAsync(
            title: "Delete Link Crypter Container Config",
            message: $"Are you sure you want to delete this link crypter container config?",
            yesText: "Delete",
            noText: "Cancel"
        );

        if (result == true)
        {
            var service = ScopedServices.GetRequiredService<UploadConfigLinkCrypterService>();
            await service.DeleteAsync(uploadConfigLinkCrypterDto.UploadConfigLinkCrypterId);
            await LoadDataAsync();
        }
    }

    private async Task LoadDataAsync()
    {
        uploadConfigLinkCrypters = await readRepository.GetByUploadConfigIdAsync(UploadConfigId);
    }

    private async Task ShowContextMenuAsync(
        DataGridRowClickEventArgs<UploadConfigLinkCrypterDto> arg
    )
    {
        selectedItem = arg.Item;
        await contextMenu.OpenMenuAsync(arg.MouseEventArgs);
    }
}
