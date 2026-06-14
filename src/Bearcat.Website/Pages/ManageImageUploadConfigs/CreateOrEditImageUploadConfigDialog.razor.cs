using Bearcat.Domain.UseCases.ManageImageUploadConfigs;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageImageUploadConfigs;

public partial class CreateOrEditImageUploadConfigDialog : OwningComponentBase
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int? ImageUploadConfigId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEdit => ImageUploadConfigId.HasValue;
    private IImageUploadConfigReadRepository readRepository = null!;
    private ImageUploadConfigFormModel formModel = null!;
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private IReadOnlyDictionary<int, string> imageHosterRegistrationOptions = null!;
    private bool isInitialized;

    private IEnumerable<SelectOption<int?>> ImageHosterRegistrationOptions =>
        imageHosterRegistrationOptions
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => new SelectOption<int?>(kvp.Key, kvp.Value));

    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<IImageUploadConfigReadRepository>();

        await InitializeFormModelAsync();
        imageHosterRegistrationOptions =
            await readRepository.GetImageHosterRegistrationOptionsAsync();

        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ImageUploadConfigService>();

        if (IsEdit)
        {
            await service.UpdateAsync(
                ImageUploadConfigId!.Value,
                formModel.Name,
                formModel.ImageHosterRegistrationId!.Value
            );
        }
        else
        {
            await service.CreateAsync(
                ReleaseId,
                formModel.Name,
                formModel.ImageHosterRegistrationId!.Value
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (formModel.ImageHosterRegistrationId is null)
        {
            messageStore.Add(
                () => formModel.ImageHosterRegistrationId!,
                L["ImageHosterRegistrationRequired"]
            );
        }
    }

    private async Task InitializeFormModelAsync()
    {
        if (!IsEdit)
        {
            formModel = new ImageUploadConfigFormModel();
            return;
        }

        var config = await readRepository.GetReadModelByIdAsync(ImageUploadConfigId!.Value);

        formModel = new ImageUploadConfigFormModel
        {
            Name = config.Name,
            ImageHosterRegistrationId = config.ImageHosterRegistrationId,
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
