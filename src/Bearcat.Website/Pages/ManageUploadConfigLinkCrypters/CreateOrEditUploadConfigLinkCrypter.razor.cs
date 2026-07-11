using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageUploadConfigLinkCrypters;

public partial class CreateOrEditUploadConfigLinkCrypter(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [Parameter]
    public int UploadConfigId { get; set; }

    [Parameter]
    public int? UploadConfigLinkCrypterId { get; set; }

    [Parameter]
    public string? ReleaseName { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<LinkCrypterOptionReadModel> linkCrypterOptions = [];
    private bool isInitialized;
    private FormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private UploadConfigLinkCrypterReadModel? configReadModel;

    private bool IsEdit => UploadConfigLinkCrypterId.HasValue;

    private LinkCrypterOptionReadModel? SelectedLinkCrypterOption =>
        formModel.LinkCrypterRegistrationId is null
            ? null
            : linkCrypterOptions.FirstOrDefault(option =>
                option.LinkCrypterRegistrationId == formModel.LinkCrypterRegistrationId.Value
            );

    private bool CanUseCaptcha => SelectedLinkCrypterOption?.SupportsCaptcha is true;
    private bool CanUseContainerDownload =>
        SelectedLinkCrypterOption?.SupportsContainerDownload is true;
    private bool CanUseClickAndLoad => SelectedLinkCrypterOption?.SupportsClickAndLoad is true;

    protected override async Task OnInitializedAsync()
    {
        linkCrypterOptions = await operationRunner.RunAsync(
            (IUploadConfigLinkCrypterReadRepository repository) =>
                repository.GetLinkCrypterOptionsAsync()
        );
        await InitializeFormModelAsync();

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        messageStore = new ValidationMessageStore(editContext);

        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        await operationRunner.RunAsync<UploadConfigLinkCrypterService>(async service =>
        {
            if (IsEdit)
            {
                await service.UpdateAsync(
                    UploadConfigLinkCrypterId!.Value,
                    formModel.Password,
                    CanUseCaptcha && formModel.EnableCaptcha,
                    CanUseContainerDownload && formModel.EnableContainerDownload,
                    CanUseClickAndLoad && formModel.EnableClickAndLoad
                );
                return;
            }

            await service.CreateAsync(
                uploadConfigId: UploadConfigId,
                linkCrypterRegistrationId: formModel.LinkCrypterRegistrationId!.Value,
                password: formModel.Password,
                enableCaptcha: CanUseCaptcha && formModel.EnableCaptcha,
                enableContainerDownload: CanUseContainerDownload
                    && formModel.EnableContainerDownload,
                enableClickAndLoad: CanUseClickAndLoad && formModel.EnableClickAndLoad
            );
        });

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

    private void OnLinkCrypterRegistrationChanged()
    {
        formModel.EnableCaptcha = CanUseCaptcha;
        formModel.EnableContainerDownload = CanUseContainerDownload;
        formModel.EnableClickAndLoad = CanUseClickAndLoad;
    }

    private async Task InitializeFormModelAsync()
    {
        if (!IsEdit)
        {
            formModel = new FormModel();
            return;
        }

        configReadModel = await operationRunner.RunAsync(
            (IUploadConfigLinkCrypterReadRepository repository) =>
                repository.GetByIdAsync(UploadConfigLinkCrypterId!.Value)
        );

        if (
            linkCrypterOptions.All(option =>
                option.LinkCrypterRegistrationId != configReadModel.LinkCrypterRegistrationId
            )
        )
        {
            linkCrypterOptions = linkCrypterOptions
                .Append(
                    new LinkCrypterOptionReadModel(
                        configReadModel.LinkCrypterRegistrationId,
                        configReadModel.LinkCrypterRegistrationName,
                        configReadModel.SupportsCaptcha,
                        configReadModel.SupportsContainerDownload,
                        configReadModel.SupportsClickAndLoad
                    )
                )
                .ToList();
        }

        formModel = new FormModel
        {
            LinkCrypterRegistrationId = configReadModel.LinkCrypterRegistrationId,
            Password = configReadModel.Password,
            EnableCaptcha = configReadModel.EnableCaptcha,
            EnableContainerDownload = configReadModel.EnableContainerDownload,
            EnableClickAndLoad = configReadModel.EnableClickAndLoad,
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
