using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.UseCases.ManageNfoDatabases;
using Bearcat.Domain.UseCases.ManageNfoDatabases.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageNfoDatabases;

public partial class CreateOrEditDialog(IScopedOperationRunner operationRunner)
{
    [Parameter]
    public int? NfoDatabaseRegistrationId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEditMode => NfoDatabaseRegistrationId.HasValue;
    private RegistrationFormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore validationMessageStore = null!;
    private IReadOnlyList<NfoDatabaseDto> nfoDatabases = [];
    private IReadOnlySet<string> registeredClassNames = new HashSet<string>();
    private readonly HashSet<string> displayedSecrets = [];
    private NfoDatabaseDto? SelectedNfoDatabase =>
        nfoDatabases.FirstOrDefault(database => database.ClassName == formModel.ClassName);
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        await InitializeFormModelAsync();
        await InitializeNfoDatabasesAsync();

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        validationMessageStore = new ValidationMessageStore(editContext);
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        await operationRunner.RunAsync<NfoDatabaseRegistrationService>(async service =>
        {
            if (!IsEditMode)
            {
                await service.CreateAsync(
                    className: formModel.ClassName!,
                    configuration: formModel.Configuration
                );
                return;
            }

            await service.UpdateAsync(
                id: NfoDatabaseRegistrationId!.Value,
                configuration: formModel.Configuration
            );
        });

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        validationMessageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.ClassName))
        {
            validationMessageStore.Add(() => formModel.ClassName!, L["SelectNfoDatabaseRequired"]);
        }

        if (
            !IsEditMode
            && !string.IsNullOrWhiteSpace(formModel.ClassName)
            && registeredClassNames.Contains(formModel.ClassName)
        )
        {
            validationMessageStore.Add(
                () => formModel.ClassName!,
                L["NfoDatabaseAlreadyRegistered"]
            );
        }

        if (SelectedNfoDatabase is null)
        {
            return;
        }

        if (IsEditMode)
        {
            return;
        }

        var missingKeys = SelectedNfoDatabase
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

        var registration = await operationRunner.RunAsync(
            (INfoDatabaseRegistrationReadRepository repository) =>
                repository.GetByIdAsync(NfoDatabaseRegistrationId!.Value)
        );

        if (registration is null)
        {
            await DialogRef.CancelAsync();
            return;
        }

        formModel = new RegistrationFormModel { ClassName = registration.NfoDatabaseClassName };
    }

    private async Task InitializeNfoDatabasesAsync()
    {
        var registrations = await operationRunner.RunAsync(
            (INfoDatabaseRegistrationReadRepository repository) => repository.GetAllAsync()
        );
        registeredClassNames = registrations
            .Where(registration =>
                !IsEditMode || registration.Id != NfoDatabaseRegistrationId!.Value
            )
            .Select(registration => registration.NfoDatabaseClassName)
            .ToHashSet();

        nfoDatabases = operationRunner.Run(
            (INfoDatabaseFactory factory) =>
                (IReadOnlyList<NfoDatabaseDto>)
                    factory
                        .GetNfoDatabases()
                        .Where(database =>
                            IsEditMode || !registeredClassNames.Contains(database.ClassName)
                        )
                        .ToList()
        );
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

    private void OnSelectedNfoDatabaseChanged()
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
            || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("key", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase);
    }
}
