using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateOrEditCollectionImageUploadConfigTemplateDialog(
    IImageUploadConfigReadRepository imageUploadConfigReadRepository
) : OwningComponentBase
{
    [Parameter]
    public ImageUploadConfigTemplateFormModel FormModel { get; set; } = null!;

    [Parameter]
    public int ReleaseTemplateId { get; set; }

    [Parameter]
    public int? CollectionImageUploadConfigTemplateId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyDictionary<int, string> imageHosterRegistrationOptions = null!;
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isInitialized;

    private bool IsEdit => CollectionImageUploadConfigTemplateId is not null;

    private IEnumerable<SelectOption<int?>> ImageHosterRegistrationOptions =>
        imageHosterRegistrationOptions
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => new SelectOption<int?>(kvp.Key, kvp.Value));

    protected override async Task OnInitializedAsync()
    {
        imageHosterRegistrationOptions =
            await imageUploadConfigReadRepository.GetImageHosterRegistrationOptionsAsync();
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();

        if (IsEdit)
        {
            await service.UpdateCollectionImageUploadConfigTemplateAsync(
                CollectionImageUploadConfigTemplateId!.Value,
                FormModel.Name,
                FormModel.ImageHosterRegistrationId!.Value
            );
        }
        else
        {
            await service.CreateCollectionImageUploadConfigTemplateAsync(
                ReleaseTemplateId,
                FormModel.Name,
                FormModel.ImageHosterRegistrationId!.Value
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (FormModel.ImageHosterRegistrationId is null)
        {
            messageStore.Add(
                () => FormModel.ImageHosterRegistrationId!,
                L["ImageHosterRegistrationRequired"]
            );
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
