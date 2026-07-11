using Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateOrEditUploadConfigLinkCrypterTemplateDialog(
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public UploadConfigLinkCrypterTemplateFormModel FormModel { get; set; } = null!;

    [Parameter]
    public int UploadConfigTemplateId { get; set; }

    [Parameter]
    public int? UploadConfigLinkCrypterTemplateId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<LinkCrypterRegistrationReadModel> linkCrypterRegistrations = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isInitialized;
    private bool isEdit;

    private LinkCrypterRegistrationReadModel? SelectedLinkCrypterRegistration =>
        FormModel.LinkCrypterRegistrationId is null
            ? null
            : linkCrypterRegistrations.FirstOrDefault(linkCrypter =>
                linkCrypter.LinkCrypterRegistrationId == FormModel.LinkCrypterRegistrationId.Value
            );

    private bool CanUseCaptcha => SelectedLinkCrypterRegistration?.SupportsCaptcha is true;
    private bool CanUseContainerDownload =>
        SelectedLinkCrypterRegistration?.SupportsContainerDownload is true;
    private bool CanUseClickAndLoad =>
        SelectedLinkCrypterRegistration?.SupportsClickAndLoad is true;

    private IReadOnlyList<SelectOption<int?>> LinkCrypterOptions =>
        linkCrypterRegistrations
            .OrderBy(linkCrypter => linkCrypter.Name)
            .Select(linkCrypter => new SelectOption<int?>(
                linkCrypter.LinkCrypterRegistrationId,
                linkCrypter.Name
            ))
            .ToList();

    private IReadOnlyList<SelectOption<LinkCrypterContainerScope>> ContainerScopeOptions =>
        Enum.GetValues<LinkCrypterContainerScope>()
            .Select(scope => new SelectOption<LinkCrypterContainerScope>(scope, L.Localize(scope)))
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isEdit = UploadConfigLinkCrypterTemplateId is not null;

        linkCrypterRegistrations = await operationRunner.RunAsync(
            (ILinkCrypterRegistrationReadRepository repository) => repository.GetAllAsync()
        );
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        await operationRunner.RunAsync<ReleaseTemplateService>(async service =>
        {
            if (isEdit)
            {
                await service.UpdateUploadConfigLinkCrypterTemplateAsync(
                    UploadConfigLinkCrypterTemplateId!.Value,
                    FormModel.Password,
                    CanUseCaptcha && FormModel.EnableCaptcha,
                    CanUseContainerDownload && FormModel.EnableContainerDownload,
                    CanUseClickAndLoad && FormModel.EnableClickAndLoad,
                    FormModel.ContainerScope
                );
                return;
            }

            await service.CreateUploadConfigLinkCrypterTemplateAsync(
                UploadConfigTemplateId,
                FormModel.LinkCrypterRegistrationId!.Value,
                FormModel.Password,
                CanUseCaptcha && FormModel.EnableCaptcha,
                CanUseContainerDownload && FormModel.EnableContainerDownload,
                CanUseClickAndLoad && FormModel.EnableClickAndLoad,
                FormModel.ContainerScope
            );
        });

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (!isEdit && FormModel.LinkCrypterRegistrationId is null)
        {
            messageStore.Add(
                () => FormModel.LinkCrypterRegistrationId!,
                L["SelectLinkCrypterRequired"]
            );
        }
    }

    private void OnLinkCrypterRegistrationChanged()
    {
        FormModel.EnableCaptcha = CanUseCaptcha;
        FormModel.EnableContainerDownload = CanUseContainerDownload;
        FormModel.EnableClickAndLoad = CanUseClickAndLoad;
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
