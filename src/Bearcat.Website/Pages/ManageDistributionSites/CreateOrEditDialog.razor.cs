using System.ComponentModel.DataAnnotations;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageDistributionSites;

public partial class CreateOrEditDialog(IScopedOperationRunner operationRunner)
{
    [Parameter]
    public int? DistributionSiteRegistrationId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEditMode => DistributionSiteRegistrationId.HasValue;
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
        await InitializeFormModelAsync();
        distributionSites = operationRunner.Run(
            (IDistributionSiteFactory factory) => factory.GetDistributionSites()
        );

        editContext = new EditContext(formModel);
        editContext.OnValidationRequested += OnValidationRequested;
        validationMessageStore = new ValidationMessageStore(editContext);
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        try
        {
            await operationRunner.RunAsync<DistributionSiteRegistrationService>(async service =>
            {
                if (!IsEditMode)
                {
                    await service.CreateAsync(
                        name: formModel.Name!,
                        className: formModel.ClassName!,
                        configuration: formModel.Configuration
                    );
                    return;
                }

                await service.UpdateAsync(
                    id: DistributionSiteRegistrationId!.Value,
                    name: formModel.Name!,
                    configuration: formModel.Configuration
                );
            });
        }
        catch (ValidationException exception)
        {
            validationMessageStore.Add(() => formModel.Configuration, exception.Message);
            editContext.NotifyValidationStateChanged();
            return;
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

        var missingFields = SelectedDistributionSite
            .ConfigurationFields.Where(field =>
                string.IsNullOrWhiteSpace(formModel.Configuration.GetValueOrDefault(field.Key))
            )
            .ToList();

        foreach (var field in missingFields)
        {
            validationMessageStore.Add(
                () => formModel.Configuration,
                L[
                    "ConfigurationValueMustBeProvided",
                    field.LabelResourceKey is { } labelResourceKey ? L[labelResourceKey] : field.Key
                ]
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
            (IDistributionSiteRegistrationReadRepository repository) =>
                repository.GetByIdAsync(DistributionSiteRegistrationId!.Value)
        );

        if (registration is null)
        {
            await DialogRef.CancelAsync();
            return;
        }

        formModel = new RegistrationFormModel
        {
            Name = registration.Name,
            ClassName = registration.DistributionSiteClassName,
            Configuration = new Dictionary<string, string>(registration.Configuration),
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
}
