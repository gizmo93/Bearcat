using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Domain.UseCases.ManageImageHosters;
using Bearcat.Domain.UseCases.ManageImageHosters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageImageHosters;

public partial class CreateOrEditDialog
{
    [Parameter]
    public int? ImageHosterRegistrationId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEditMode => ImageHosterRegistrationId.HasValue;
    private IImageHosterRegistrationReadRepository readRepository = null!;
    private IImageHosterFactory imageHosterFactory = null!;
    private RegistrationFormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore validationMessageStore = null!;
    private IReadOnlyList<ImageHosterDto> imageHosters = [];
    private readonly HashSet<string> displayedSecrets = [];
    private ImageHosterDto? SelectedImageHoster =>
        imageHosters.FirstOrDefault(imageHoster => imageHoster.ClassName == formModel.ClassName);
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        readRepository =
            ScopedServices.GetRequiredService<IImageHosterRegistrationReadRepository>();
        imageHosterFactory = ScopedServices.GetRequiredService<IImageHosterFactory>();

        await InitializeFormModelAsync();
        imageHosters = imageHosterFactory.GetImageHosters();

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        validationMessageStore = new ValidationMessageStore(editContext);
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ImageHosterService>();

        if (!IsEditMode)
        {
            await service.CreateAsync(
                name: formModel.Name!,
                className: formModel.ClassName!,
                configuration: formModel.Configuration
            );
        }
        else
        {
            await service.UpdateAsync(
                id: ImageHosterRegistrationId!.Value,
                name: formModel.Name!,
                configuration: formModel.Configuration
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        validationMessageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            validationMessageStore.Add(() => formModel.Name!, L["NameIsRequired"]);
        }

        if (string.IsNullOrWhiteSpace(formModel.ClassName))
        {
            validationMessageStore.Add(() => formModel.ClassName!, L["SelectImageHosterRequired"]);
        }

        if (SelectedImageHoster is null || IsEditMode)
        {
            return;
        }

        var missingKeys = SelectedImageHoster
            .ConfigurationKeys.Where(key =>
                string.IsNullOrWhiteSpace(formModel.Configuration.GetValueOrDefault(key))
            )
            .ToList();

        foreach (var key in missingKeys)
        {
            validationMessageStore.Add(
                () => formModel.Configuration,
                L["ConfigurationValueMustBeProvided", key]
            );
        }
    }

    private async Task InitializeFormModelAsync()
    {
        if (!IsEditMode)
        {
            formModel = new RegistrationFormModel();
            return;
        }

        var registration = await readRepository.GetByIdAsync(ImageHosterRegistrationId!.Value);

        if (registration is null)
        {
            await DialogRef.CancelAsync();
            return;
        }

        formModel = new RegistrationFormModel
        {
            Name = registration.Name,
            ClassName = registration.ImageHosterClassName,
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private void ToggleShowHideSecret(string key)
    {
        if (displayedSecrets.Add(key))
        {
            return;
        }

        displayedSecrets.Remove(key);
    }

    private void OnSelectedImageHosterChanged()
    {
        if (IsEditMode)
        {
            return;
        }

        displayedSecrets.Clear();
        formModel.Configuration = new Dictionary<string, string>();
    }

    private void OnConfigurationValueChanged(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            formModel.Configuration.Remove(key);
            return;
        }

        formModel.Configuration[key] = value;
    }

    private static bool IsSecretField(string key)
    {
        return key.Contains("password", StringComparison.OrdinalIgnoreCase)
            || key.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || key.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase);
    }
}
