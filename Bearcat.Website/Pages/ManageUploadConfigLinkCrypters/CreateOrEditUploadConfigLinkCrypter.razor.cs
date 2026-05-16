using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageUploadConfigLinkCrypters;

public partial class CreateOrEditUploadConfigLinkCrypter : OwningComponentBase
{
    [Parameter]
    public int UploadConfigId { get; set; }

    [Parameter]
    public int? UploadConfigLinkCrypterId { get; set; }

    [Parameter]
    public string? ReleaseName { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyDictionary<int, string> linkCrypterOptions = new Dictionary<int, string>();
    private bool isInitialized;
    private FormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private IUploadConfigLinkCrypterReadRepository readRepository = null!;

    private bool IsEdit => UploadConfigLinkCrypterId.HasValue;

    protected override async Task OnInitializedAsync()
    {
        readRepository =
            ScopedServices.GetRequiredService<IUploadConfigLinkCrypterReadRepository>();

        linkCrypterOptions = await readRepository.GetLinkCrypterOptionsAsync();
        await InitializeFormModelAsync();

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        messageStore = new ValidationMessageStore(editContext);

        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<UploadConfigLinkCrypterService>();
        if (IsEdit)
        {
            await service.UpdateAsync(UploadConfigLinkCrypterId!.Value, formModel.Password);
        }
        else
        {
            await service.CreateAsync(
                uploadConfigId: UploadConfigId,
                linkCrypterRegistrationId: formModel.LinkCrypterRegistrationId!.Value,
                password: formModel.Password
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (formModel.LinkCrypterRegistrationId is null)
        {
            messageStore.Add(
                () => formModel.LinkCrypterRegistrationId!,
                L["SelectLinkCrypterRequired"]
            );
        }
    }

    private async Task InitializeFormModelAsync()
    {
        if (!IsEdit)
        {
            formModel = new FormModel();
            return;
        }

        var configDto = await readRepository.GetByIdAsync(UploadConfigLinkCrypterId!.Value);

        formModel = new FormModel
        {
            LinkCrypterRegistrationId = configDto.LinkCrypterRegistrationId,
            Password = configDto.Password,
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
