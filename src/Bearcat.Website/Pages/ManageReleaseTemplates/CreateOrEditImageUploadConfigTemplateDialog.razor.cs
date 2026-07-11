using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateOrEditImageUploadConfigTemplateDialog(
    IScopedOperationRunner operationRunner
) : ComponentBase
{
    [Parameter]
    public ImageUploadConfigTemplateFormModel FormModel { get; set; } = null!;

    [Parameter]
    public int ReleaseTemplateId { get; set; }

    [Parameter]
    public int? ImageUploadConfigTemplateId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyDictionary<int, string> imageHosterRegistrationOptions = null!;
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isInitialized;

    private bool IsEdit => ImageUploadConfigTemplateId is not null;

    private IReadOnlyList<SelectOption<int?>> ImageHosterRegistrationOptions =>
        imageHosterRegistrationOptions
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => new SelectOption<int?>(kvp.Key, kvp.Value))
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        imageHosterRegistrationOptions = await operationRunner.RunAsync(
            (IImageUploadConfigReadRepository repository) =>
                repository.GetImageHosterRegistrationOptionsAsync()
        );
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        await operationRunner.RunAsync<ReleaseTemplateService>(async service =>
        {
            if (IsEdit)
            {
                await service.UpdateImageUploadConfigTemplateAsync(
                    ImageUploadConfigTemplateId!.Value,
                    FormModel.Name,
                    FormModel.ImageHosterRegistrationId!.Value
                );
                return;
            }

            await service.CreateImageUploadConfigTemplateAsync(
                ReleaseTemplateId,
                FormModel.Name,
                FormModel.ImageHosterRegistrationId!.Value
            );
        });

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
