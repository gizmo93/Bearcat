using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.UseCases.ManageSeriesDatabases;
using Bearcat.Domain.UseCases.ManageSeriesDatabases.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageSeriesDatabases;

public partial class CreateOrEditDialog
{
    [Parameter]
    public int? SeriesDatabaseRegistrationId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEditMode => SeriesDatabaseRegistrationId.HasValue;
    private ISeriesDatabaseRegistrationReadRepository readRepository = null!;
    private IMediaMetadataDatabaseFactory metadataDatabaseFactory = null!;
    private RegistrationFormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore validationMessageStore = null!;
    private IReadOnlyList<MediaMetadataDatabaseDto> seriesDatabases = [];
    private IReadOnlySet<string> registeredClassNames = new HashSet<string>();
    private readonly HashSet<string> displayedSecrets = [];
    private MediaMetadataDatabaseDto? SelectedSeriesDatabase =>
        seriesDatabases.FirstOrDefault(database => database.ClassName == formModel.ClassName);
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        readRepository =
            ScopedServices.GetRequiredService<ISeriesDatabaseRegistrationReadRepository>();
        metadataDatabaseFactory =
            ScopedServices.GetRequiredService<IMediaMetadataDatabaseFactory>();

        await InitializeFormModelAsync();
        await InitializeSeriesDatabasesAsync();

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        validationMessageStore = new ValidationMessageStore(editContext);
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<SeriesDatabaseRegistrationService>();

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
                id: SeriesDatabaseRegistrationId!.Value,
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
                L["SelectSeriesDatabaseRequired"]
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
                L["SeriesDatabaseAlreadyRegistered"]
            );
        }

        if (SelectedSeriesDatabase is null)
        {
            return;
        }

        if (IsEditMode)
        {
            return;
        }

        var missingKeys = SelectedSeriesDatabase
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

        var registration = await readRepository.GetByIdAsync(SeriesDatabaseRegistrationId!.Value);

        if (registration is null)
        {
            await DialogRef.CancelAsync();
            return;
        }

        formModel = new RegistrationFormModel { ClassName = registration.SeriesDatabaseClassName };
    }

    private async Task InitializeSeriesDatabasesAsync()
    {
        var registrations = await readRepository.GetAllAsync();
        registeredClassNames = registrations
            .Where(registration =>
                !IsEditMode || registration.Id != SeriesDatabaseRegistrationId!.Value
            )
            .Select(registration => registration.SeriesDatabaseClassName)
            .ToHashSet();

        seriesDatabases = metadataDatabaseFactory
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

    private void OnSelectedSeriesDatabaseChanged()
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
