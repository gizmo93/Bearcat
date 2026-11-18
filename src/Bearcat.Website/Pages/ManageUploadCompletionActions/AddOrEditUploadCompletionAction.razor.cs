using Bearcat.Abstractions.UploadCompletionAction;
using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.ManageUploadCompletionActions;
using Bearcat.Domain.UseCases.ManageUploadCompletionActions.ReadModels;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageUploadCompletionActions;

public partial class AddOrEditUploadCompletionAction(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    private const string ConfigResourcePrefix = "UploadCompletionActionConfig_";
    private const string NameKey = "Registration.Name";
    private const string RunForInitialUploadKey = "Registration.RunForInitialUpload";
    private const string RunForReuploadKey = "Registration.RunForReupload";
    private const int MaxNameLength = 100;

    private static readonly string[] RegistrationKeys =
    [
        NameKey,
        RunForInitialUploadKey,
        RunForReuploadKey,
    ];

    [Parameter]
    public UploadCompletionActionRegistrationReadModel? Registration { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<UploadCompletionActionDto> uploadCompletionActions = [];
    private string? selectedActionClassName;
    private FormSchema? schema;
    private Dictionary<string, object?> values = new();
    private string? errorMessage;
    private bool isSaving;

    private bool IsEdit => Registration is not null;

    private bool KeepsStoredConfigurationValues => Registration is { HasUnreadableSecrets: false };

    protected override async Task OnInitializedAsync()
    {
        uploadCompletionActions = operationRunner.Run(
            (IUploadCompletionActionFactory factory) => factory.GetUploadCompletionActions()
        );

        if (Registration is null)
        {
            return;
        }

        var storedConfigValues = KeepsStoredConfigurationValues
            ? await operationRunner.RunAsync(
                (UploadCompletionActionRegistrationService service) =>
                    service.GetConfigValuesWithoutSecretsAsync(Registration.Id)
            )
            : new Dictionary<string, object?>();

        selectedActionClassName = Registration.ActionClassName;

        values = new Dictionary<string, object?>(storedConfigValues)
        {
            [NameKey] = Registration.Name,
            [RunForInitialUploadKey] = Registration.RunForInitialUpload,
            [RunForReuploadKey] = Registration.RunForReupload,
        };

        schema = BuildSchema(GetSelectedAction());
    }

    private void OnActionTypeChanged(string? actionClassName)
    {
        selectedActionClassName = actionClassName;
        errorMessage = null;

        values = values
            .Where(entry => RegistrationKeys.Contains(entry.Key) && entry.Value is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        schema = string.IsNullOrEmpty(actionClassName) ? null : BuildSchema(GetSelectedAction());
    }

    private UploadCompletionActionDto GetSelectedAction()
    {
        return uploadCompletionActions.First(action => action.ClassName == selectedActionClassName);
    }

    private FormSchema BuildSchema(UploadCompletionActionDto uploadCompletionAction)
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
                            Name = RunForInitialUploadKey,
                            Label = L["RunForInitialUpload"],
                            Description = L["RunForInitialUploadHelp"],
                            Type = FieldType.Checkbox,
                            DefaultValue = true,
                        },
                        new FormFieldDefinition
                        {
                            Name = RunForReuploadKey,
                            Label = L["RunForReupload"],
                            Description = L["RunForReuploadHelp"],
                            Type = FieldType.Checkbox,
                            DefaultValue = true,
                        },
                    ],
                },
                new FormSectionDefinition
                {
                    Title = L["Configuration"],
                    Fields = uploadCompletionAction
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
        var runForInitialUpload = submittedValues.GetValueOrDefault(RunForInitialUploadKey) is true;
        var runForReupload = submittedValues.GetValueOrDefault(RunForReuploadKey) is true;

        var configValues = submittedValues
            .Where(entry => !RegistrationKeys.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        try
        {
            if (Registration is null)
            {
                await operationRunner.RunAsync(
                    (UploadCompletionActionRegistrationService service) =>
                        service.CreateAsync(
                            name: name,
                            actionClassName: selectedActionClassName!,
                            values: configValues,
                            runForInitialUpload: runForInitialUpload,
                            runForReupload: runForReupload
                        )
                );
            }
            else
            {
                await operationRunner.RunAsync(
                    (UploadCompletionActionRegistrationService service) =>
                        service.UpdateAsync(
                            id: Registration.Id,
                            name: name,
                            values: configValues,
                            runForInitialUpload: runForInitialUpload,
                            runForReupload: runForReupload
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
        catch (ArgumentException exception)
        {
            errorMessage = exception.Message;

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
