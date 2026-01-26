using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.UseCases.ManageLinkCrypters;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageLinkCrypters;

public partial class CreateOrEditDialog
{
    [Parameter]
    public int? LinkCrypterRegistrationId { get; set; }

    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;

    private bool IsEditMode => LinkCrypterRegistrationId.HasValue;

    private ILinkCrypterRegistrationReadRepository readRepository = null!;

    private ILinkCrypterFactory linkCrypterFactory = null!;

    private RegistrationFormModel formModel = new();

    private EditContext editContext = null!;

    private ValidationMessageStore validationMessageStore = null!;

    private IReadOnlyList<LinkCrypterDto> crypters = [];

    private HashSet<string> displayedPasswords = new();

    private LinkCrypterDto? SelectedCrypter => crypters
        .FirstOrDefault(c => c.ClassName == formModel.ClassName);

    private bool isInitialized;



    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<ILinkCrypterRegistrationReadRepository>();
        linkCrypterFactory = ScopedServices.GetRequiredService<ILinkCrypterFactory>();

        await InitializeFormModelAsync();
        InitializeCrypters();

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
                configuration: formModel.Configuration);
        }
        else
        {
            await service.UpdateAsync(
                id: LinkCrypterRegistrationId!.Value,
                name: formModel.Name!,
                configuration: formModel.Configuration);
        }

        MudDialog.Close();
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        validationMessageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            validationMessageStore.Add(
                () => formModel.Name!,
                "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(formModel.ClassName))
        {
            validationMessageStore.Add(
                () => formModel.ClassName!,
                "You need to select a link crypter");
        }

        if (string.IsNullOrWhiteSpace(formModel.ClassName))
        {
            return;
        }

        var configuredKeys = formModel
            .Configuration
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToHashSet();

        var missingKeys = SelectedCrypter!
            .ConfigurationKeys
            .Where(key => !configuredKeys.Contains(key))
            .ToList();

        foreach (var key in missingKeys)
        {
            validationMessageStore.Add(() => formModel.Configuration, $"You need to provide a value for '{key}'");
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
            MudDialog.Cancel();
        }

        formModel = new RegistrationFormModel
        {
            Name = registration!.Name,
            ClassName = registration.LinkCrypterClassName,
            Configuration = registration.Configuration
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        StateHasChanged();
    }

    private void ToggleShowHidePassword(string key)
    {
        if (displayedPasswords.Add(key))
        {
            return;
        }

        displayedPasswords.Remove(key);
    }

    private void InitializeCrypters()
    {
        crypters = linkCrypterFactory.GetLinkCrypters();
    }

    private void OnSelectedCrypterChanged()
    {
        if (IsEditMode)
        {
            return;
        }

        var crypter = SelectedCrypter;

        if (crypter is null)
        {
            formModel.Configuration = new Dictionary<string, string>();
            return;
        }

        formModel.Configuration = crypter.ConfigurationKeys
            .ToDictionary(k => k, k => string.Empty);
    }
}

