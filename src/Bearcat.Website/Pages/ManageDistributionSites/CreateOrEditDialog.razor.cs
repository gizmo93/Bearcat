using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageDistributionSites;

public partial class CreateOrEditDialog(IScopedOperationRunner operationRunner) : ComponentBase
{
    private const string ConfigResourcePrefix = "DistributionSiteConfig_";
    private const string NameKey = "Registration.Name";
    private const int MaxNameLength = 100;

    [Parameter]
    public DistributionSiteRegistrationReadModel? Registration { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<DistributionSiteDto> distributionSites = [];
    private string? selectedClassName;
    private FormSchema? schema;
    private Dictionary<string, object?> values = new();
    private string? errorMessage;
    private bool isSaving;

    private bool IsEdit => Registration is not null;

    private bool KeepsStoredConfigurationValues => Registration is { HasUnreadableSecrets: false };

    protected override async Task OnInitializedAsync()
    {
        distributionSites = operationRunner.Run(
            (IDistributionSiteFactory factory) => factory.GetDistributionSites()
        );

        if (Registration is null)
        {
            return;
        }

        var storedConfigValues = KeepsStoredConfigurationValues
            ? await operationRunner.RunAsync(
                (DistributionSiteRegistrationService service) =>
                    service.GetConfigValuesWithoutSecretsAsync(
                        Registration.DistributionSiteRegistrationId
                    )
            )
            : new Dictionary<string, object?>();

        selectedClassName = Registration.DistributionSiteClassName;
        values = new Dictionary<string, object?>(storedConfigValues)
        {
            [NameKey] = Registration.Name,
        };
        schema = BuildSchema(GetSelectedDistributionSite());
    }

    private void OnDistributionSiteChanged(string? className)
    {
        selectedClassName = className;
        errorMessage = null;
        values = values
            .Where(entry => entry.Key is NameKey && entry.Value is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        schema = string.IsNullOrEmpty(className)
            ? null
            : BuildSchema(GetSelectedDistributionSite());
    }

    private DistributionSiteDto GetSelectedDistributionSite()
    {
        return distributionSites.First(distributionSite =>
            distributionSite.ClassName == selectedClassName
        );
    }

    private FormSchema BuildSchema(DistributionSiteDto distributionSite)
    {
        return new FormSchema
        {
            Sections =
            [
                new FormSectionDefinition
                {
                    Fields =
                    [
                        new FormFieldDefinition
                        {
                            Name = NameKey,
                            Label = L["Name"],
                            Placeholder = L["ConfigurationNamePlaceholder"],
                            Type = FieldType.Text,
                            Required = true,
                            Validations =
                            [
                                new FieldValidation
                                {
                                    Type = ValidationType.MaxLength,
                                    Value = MaxNameLength,
                                },
                            ],
                        },
                    ],
                },
                new FormSectionDefinition
                {
                    Title = L["Configuration"],
                    Fields = distributionSite
                        .ConfigurationFields.Select(field =>
                            ConfigurationFieldFormMapper.ToFormField(
                                field,
                                L,
                                ConfigResourcePrefix,
                                KeepsStoredConfigurationValues
                            )
                        )
                        .ToList(),
                },
            ],
        };
    }

    private async Task SaveAsync(Dictionary<string, object?> submittedValues)
    {
        errorMessage = null;
        isSaving = true;

        var name = submittedValues.GetValueOrDefault(NameKey) as string ?? string.Empty;
        var configValues = submittedValues
            .Where(entry => entry.Key is not NameKey)
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        try
        {
            if (Registration is null)
            {
                await operationRunner.RunAsync(
                    (DistributionSiteRegistrationService service) =>
                        service.CreateAsync(
                            name: name,
                            className: selectedClassName!,
                            values: configValues
                        )
                );
            }
            else
            {
                await operationRunner.RunAsync(
                    (DistributionSiteRegistrationService service) =>
                        service.UpdateAsync(
                            id: Registration.DistributionSiteRegistrationId,
                            name: name,
                            values: configValues
                        )
                );
            }
        }
        catch (ConfigurationFieldValidationException exception)
        {
            errorMessage = ConfigurationFieldFormMapper.CreateFieldErrorMessage(
                exception,
                L,
                ConfigResourcePrefix
            );

            return;
        }
        finally
        {
            isSaving = false;
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
