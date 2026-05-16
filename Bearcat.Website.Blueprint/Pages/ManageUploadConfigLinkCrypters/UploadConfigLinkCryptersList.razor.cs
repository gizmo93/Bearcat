using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Blueprint.Pages.ManageUploadConfigLinkCrypters;

public partial class UploadConfigLinkCryptersList(DialogService dialogService) : OwningComponentBase
{
    [Parameter]
    [EditorRequired]
    public int UploadConfigId { get; set; }

    [Parameter]
    [EditorRequired]
    public string? ReleaseName { get; set; }

    private IUploadConfigLinkCrypterReadRepository readRepository = null!;
    private IReadOnlyList<UploadConfigLinkCrypterDto> uploadConfigLinkCrypters = [];
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
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditUploadConfigLinkCrypter.UploadConfigId)] = UploadConfigId,
            [nameof(CreateOrEditUploadConfigLinkCrypter.ReleaseName)] = ReleaseName,
        };

        if (config is not null)
        {
            parameters[nameof(CreateOrEditUploadConfigLinkCrypter.UploadConfigLinkCrypterId)] =
                config.UploadConfigLinkCrypterId;
        }

        var dialogTitle = config is null
            ? "Add Link Crypter Container"
            : "Edit Link Crypter Container";

        var dialog = await dialogService.OpenAsync<CreateOrEditUploadConfigLinkCrypter>(
            parameters,
            new DialogOpenOptions
            {
                Title = dialogTitle,
                Description = "Attach a link crypter to this upload configuration.",
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadDataAsync();
        }
    }

    private async Task ShowDeleteDialogAsync(UploadConfigLinkCrypterDto uploadConfigLinkCrypterDto)
    {
        var result = await dialogService.ConfirmAsync(
            "Delete Link Crypter Container Config",
            "Are you sure you want to delete this link crypter container config?",
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

        var service = ScopedServices.GetRequiredService<UploadConfigLinkCrypterService>();
        await service.DeleteAsync(uploadConfigLinkCrypterDto.UploadConfigLinkCrypterId);
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        uploadConfigLinkCrypters = await readRepository.GetByUploadConfigIdAsync(UploadConfigId);
    }
}
