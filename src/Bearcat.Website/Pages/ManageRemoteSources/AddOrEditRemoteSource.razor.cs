using System.Globalization;
using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.ManageRemoteSources;
using Bearcat.Domain.UseCases.ManageRemoteSources.ReadModels;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageRemoteSources;

public partial class AddOrEditRemoteSource(IScopedOperationRunner operationRunner) : ComponentBase
{
    private const string ConfigResourcePrefix = "RemoteSourceConfig_";
    private const string NameKey = "Registration.Name";
    private const string MaxConnectionsKey = "Registration.MaxConnections";
    private const int MaxNameLength = 100;

    [Parameter]
    public RemoteSourceRegistrationReadModel? Registration { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<RemoteSourceDto> remoteSources = [];
    private string? selectedSourceClassName;
    private FormSchema? schema;
    private Dictionary<string, object?> values = new();
    private string? errorMessage;
    private bool isSaving;

    private bool IsEdit => Registration is not null;

    protected override async Task OnInitializedAsync()
    {
        remoteSources = operationRunner.Run(
            (IRemoteSourceFactory factory) => factory.GetRemoteSources()
        );

        if (Registration is null)
        {
            return;
        }

        var editValues = await operationRunner.RunAsync(
            (RemoteSourceRegistrationService service) => service.GetEditValuesAsync(Registration.Id)
        );

        selectedSourceClassName = Registration.SourceClassName;
        values = new Dictionary<string, object?>(editValues)
        {
            [NameKey] = Registration.Name,
            [MaxConnectionsKey] = Registration.MaxConnections,
        };
        schema = BuildSchema(GetSelectedSource());
    }

    private void OnSourceTypeChanged(string? sourceClassName)
    {
        selectedSourceClassName = sourceClassName;
        errorMessage = null;
        values = values
            .Where(entry => entry.Key is NameKey or MaxConnectionsKey && entry.Value is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        schema = string.IsNullOrEmpty(sourceClassName) ? null : BuildSchema(GetSelectedSource());
    }

    private RemoteSourceDto GetSelectedSource()
    {
        return remoteSources.First(source => source.ClassName == selectedSourceClassName);
    }

    private FormSchema BuildSchema(RemoteSourceDto remoteSource)
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
                        new FormFieldDefinition
                        {
                            Name = MaxConnectionsKey,
                            Label = L["MaxConnections"],
                            Description = L["MaxConnectionsHelp"],
                            Type = FieldType.Number,
                            Required = true,
                            DefaultValue = RemoteSourceRegistration.DefaultMaxConnections,
                            Validations =
                            [
                                new FieldValidation
                                {
                                    Type = ValidationType.Min,
                                    Value = RemoteSourceRegistrationService.MinMaxConnections,
                                },
                                new FieldValidation { Type = ValidationType.Custom },
                            ],
                            Metadata = ConfigurationFieldFormMapper.CreateWholeNumberMetadata(
                                L["MustBeWholeNumber"],
                                RemoteSourceRegistrationService.MinMaxConnections
                            ),
                        },
                    ],
                },
                new FormSectionDefinition
                {
                    Title = L["Configuration"],
                    Fields = remoteSource
                        .ConfigurationFields.Select(field =>
                            ConfigurationFieldFormMapper.ToFormField(
                                field,
                                L,
                                ConfigResourcePrefix,
                                IsEdit
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
        var maxConnections = Convert.ToInt32(
            submittedValues[MaxConnectionsKey],
            CultureInfo.InvariantCulture
        );
        var configValues = submittedValues
            .Where(entry => entry.Key is not NameKey and not MaxConnectionsKey)
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        try
        {
            if (Registration is null)
            {
                await operationRunner.RunAsync(
                    (RemoteSourceRegistrationService service) =>
                        service.CreateAsync(
                            name,
                            selectedSourceClassName!,
                            configValues,
                            maxConnections
                        )
                );
            }
            else
            {
                await operationRunner.RunAsync(
                    (RemoteSourceRegistrationService service) =>
                        service.UpdateAsync(Registration.Id, name, configValues, maxConnections)
                );
            }
        }
        catch (ConfigurationFieldValidationException exception)
        {
            errorMessage = DescribeFieldError(exception);

            return;
        }
        catch (ArgumentException exception)
        {
            errorMessage = DescribeValidationError(exception);

            return;
        }
        finally
        {
            isSaving = false;
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private string DescribeFieldError(ConfigurationFieldValidationException exception)
    {
        var label = L[$"{ConfigResourcePrefix}{exception.FieldKey}"];
        var fieldName = label.ResourceNotFound ? exception.FieldKey : label.Value;

        return L[$"ConfigurationFieldError_{exception.Error}", fieldName];
    }

    private static string DescribeValidationError(ArgumentException exception)
    {
        var index = exception.Message.IndexOf(" (Parameter", StringComparison.Ordinal);

        return index < 0 ? exception.Message : exception.Message[..index];
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
