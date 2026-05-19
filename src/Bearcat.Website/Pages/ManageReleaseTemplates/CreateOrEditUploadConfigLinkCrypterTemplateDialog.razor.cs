using Bearcat.Domain.UseCases.ManageLinkCrypters.Dto;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateOrEditUploadConfigLinkCrypterTemplateDialog(
    ILinkCrypterRegistrationReadRepository linkCrypterReadRepository
) : OwningComponentBase
{
    [Parameter]
    public UploadConfigLinkCrypterTemplateFormModel FormModel { get; set; } = null!;

    [Parameter]
    public int UploadConfigTemplateId { get; set; }

    [Parameter]
    public int? UploadConfigLinkCrypterTemplateId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<LinkCrypterRegistrationDto> linkCrypterRegistrations = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isInitialized;
    private bool isEdit;

    private IEnumerable<SelectOption<int?>> LinkCrypterOptions =>
        linkCrypterRegistrations
            .OrderBy(linkCrypter => linkCrypter.Name)
            .Select(linkCrypter => new SelectOption<int?>(
                linkCrypter.LinkCrypterRegistrationId,
                linkCrypter.Name
            ));

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isEdit = UploadConfigLinkCrypterTemplateId is not null;

        linkCrypterRegistrations = await linkCrypterReadRepository.GetAllAsync();
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();

        if (isEdit)
        {
            await service.UpdateUploadConfigLinkCrypterTemplateAsync(
                UploadConfigLinkCrypterTemplateId!.Value,
                FormModel.Password
            );
        }
        else
        {
            await service.CreateUploadConfigLinkCrypterTemplateAsync(
                UploadConfigTemplateId,
                FormModel.LinkCrypterRegistrationId!.Value,
                FormModel.Password
            );
        }

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

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
