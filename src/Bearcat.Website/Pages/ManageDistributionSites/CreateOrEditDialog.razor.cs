using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageDistributionSites;

public partial class CreateOrEditDialog
{
    [Parameter]
    public int? DistributionSiteRegistrationId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEditMode => DistributionSiteRegistrationId.HasValue;
    private IDistributionSiteRegistrationReadRepository readRepository = null!;
    private IDistributionSiteFactory distributionSiteFactory = null!;
    private RegistrationFormModel formModel = new();
    private EditContext editContext = null!;
    private ValidationMessageStore validationMessageStore = null!;
    private IReadOnlyList<DistributionSiteDto> distributionSites = [];
    private readonly HashSet<string> displayedSecrets = [];
    private DistributionSiteDto? SelectedDistributionSite =>
        distributionSites.FirstOrDefault(distributionSite =>
            distributionSite.ClassName == formModel.ClassName
        );
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        readRepository =
            ScopedServices.GetRequiredService<IDistributionSiteRegistrationReadRepository>();
        distributionSiteFactory = ScopedServices.GetRequiredService<IDistributionSiteFactory>();

        await InitializeFormModelAsync();
        distributionSites = distributionSiteFactory.GetDistributionSites();

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        validationMessageStore = new ValidationMessageStore(editContext);
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<DistributionSiteRegistrationService>();

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
                id: DistributionSiteRegistrationId!.Value,
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
            validationMessageStore.Add(
                () => formModel.ClassName!,
                L["SelectDistributionSiteRequired"]
            );
        }

        if (SelectedDistributionSite is null || IsEditMode)
        {
            return;
        }

        var missingKeys = SelectedDistributionSite
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

        var registration = await readRepository.GetByIdAsync(DistributionSiteRegistrationId!.Value);

        if (registration is null)
        {
            await DialogRef.CancelAsync();
            return;
        }

        formModel = new RegistrationFormModel
        {
            Name = registration.Name,
            ClassName = registration.DistributionSiteClassName,
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

    private void OnSelectedDistributionSiteChanged()
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
            || key.Contains("token", StringComparison.OrdinalIgnoreCase);
    }
}
