using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.UseCases.ManageLinkCrypters;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageLinkCrypters;

public partial class CreateOrEditDialog
{
    [Parameter]
    public int? LinkCrypterRegistrationId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEditMode => LinkCrypterRegistrationId.HasValue;
    private ILinkCrypterRegistrationReadRepository readRepository = null!;
    private ILinkCrypterFactory linkCrypterFactory = null!;
    private RegistrationFormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore validationMessageStore = null!;
    private IReadOnlyList<LinkCrypterDto> crypters = [];
    private readonly HashSet<string> displayedPasswords = [];
    private LinkCrypterDto? SelectedCrypter =>
        crypters.FirstOrDefault(c => c.ClassName == formModel.ClassName);
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        readRepository =
            ScopedServices.GetRequiredService<ILinkCrypterRegistrationReadRepository>();
        linkCrypterFactory = ScopedServices.GetRequiredService<ILinkCrypterFactory>();

        await InitializeFormModelAsync();
        crypters = linkCrypterFactory.GetLinkCrypters();

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        validationMessageStore = new ValidationMessageStore(editContext);
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<LinkCrypterService>();

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
                id: LinkCrypterRegistrationId!.Value,
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
            validationMessageStore.Add(() => formModel.ClassName!, L["SelectLinkCrypterRequired"]);
        }

        if (SelectedCrypter is null)
        {
            return;
        }

        var missingKeys = SelectedCrypter
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

        var registration = await readRepository.GetByIdAsync(LinkCrypterRegistrationId!.Value);

        if (registration is null)
        {
            await DialogRef.CancelAsync();
            return;
        }

        formModel = new RegistrationFormModel
        {
            Name = registration.Name,
            ClassName = registration.LinkCrypterClassName,
            Configuration = registration.Configuration.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            ),
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private void ToggleShowHidePassword(string key)
    {
        if (displayedPasswords.Add(key))
        {
            return;
        }

        displayedPasswords.Remove(key);
    }

    private void OnSelectedCrypterChanged()
    {
        if (IsEditMode)
        {
            return;
        }

        displayedPasswords.Clear();
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
}
