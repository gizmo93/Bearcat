using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.UseCases.ManageMediaDatabases;
using Bearcat.Domain.UseCases.ManageMediaDatabases.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageMediaDatabases;

public partial class CreateOrEditDialog
{
    [Parameter]
    public int? MediaDatabaseRegistrationId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEditMode => MediaDatabaseRegistrationId.HasValue;
    private IMediaDatabaseRegistrationReadRepository readRepository = null!;
    private IMediaMetadataDatabaseFactory metadataDatabaseFactory = null!;
    private RegistrationFormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore validationMessageStore = null!;
    private IReadOnlyList<MediaMetadataDatabaseDto> mediaDatabases = [];
    private IReadOnlyList<string> registeredClassNames = [];
    private readonly HashSet<string> displayedSecrets = [];
    private MediaMetadataDatabaseDto? SelectedMediaDatabase =>
        mediaDatabases.FirstOrDefault(database => database.ClassName == formModel.ClassName);
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        readRepository =
            ScopedServices.GetRequiredService<IMediaDatabaseRegistrationReadRepository>();
        metadataDatabaseFactory =
            ScopedServices.GetRequiredService<IMediaMetadataDatabaseFactory>();

        await InitializeFormModelAsync();
        await InitializeMediaDatabasesAsync();

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        validationMessageStore = new ValidationMessageStore(editContext);
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<MediaDatabaseRegistrationService>();

        if (!IsEditMode)
        {
            await service.CreateAsync(
                className: formModel.ClassName!,
                configuration: formModel.Configuration
            );
        }
        else
        {
            await service.UpdateAsync(
                id: MediaDatabaseRegistrationId!.Value,
                configuration: formModel.Configuration
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        validationMessageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.ClassName))
        {
            validationMessageStore.Add(
                () => formModel.ClassName!,
                L["SelectMediaDatabaseRequired"]
            );
        }

        if (
            !IsEditMode
            && !string.IsNullOrWhiteSpace(formModel.ClassName)
            && registeredClassNames.Contains(formModel.ClassName)
        )
        {
            validationMessageStore.Add(
                () => formModel.ClassName!,
                L["MediaDatabaseAlreadyRegistered"]
            );
        }

        if (SelectedMediaDatabase is null)
        {
            return;
        }

        if (IsEditMode)
        {
            return;
        }

        var missingKeys = SelectedMediaDatabase
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

        var registration = await readRepository.GetByIdAsync(MediaDatabaseRegistrationId!.Value);

        if (registration is null)
        {
            await DialogRef.CancelAsync();
            return;
        }

        formModel = new RegistrationFormModel { ClassName = registration.MediaDatabaseClassName };
    }

    private async Task InitializeMediaDatabasesAsync()
    {
        var registrations = await readRepository.GetAllAsync();
        registeredClassNames = registrations
            .Where(registration =>
                !IsEditMode || registration.Id != MediaDatabaseRegistrationId!.Value
            )
            .Select(registration => registration.MediaDatabaseClassName)
            .ToList();

        mediaDatabases = metadataDatabaseFactory
            .GetDatabases()
            .Where(database => IsEditMode || !registeredClassNames.Contains(database.ClassName))
            .ToList();
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

    private void OnSelectedMediaDatabaseChanged()
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
