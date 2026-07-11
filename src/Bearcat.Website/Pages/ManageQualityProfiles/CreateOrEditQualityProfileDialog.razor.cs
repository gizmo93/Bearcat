using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.UseCases.ManageQualityProfiles;
using Bearcat.Domain.UseCases.ManageQualityProfiles.Dto;
using Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageQualityProfiles;

public partial class CreateOrEditQualityProfileDialog(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    public QualityProfileFormModel FormModel { get; set; } = new();

    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;

    private IReadOnlyList<QualityCheckRuleType> ruleTypes = [];
    private IReadOnlyDictionary<
        QualityCheckRuleType,
        IReadOnlyList<QualityCheckParameterDescriptor>
    > parameterDescriptors =
        new Dictionary<QualityCheckRuleType, IReadOnlyList<QualityCheckParameterDescriptor>>();
    private List<QualityCheckRuleEditModel> rules = [];

    private IReadOnlyList<SelectOption<QualityCheckRuleType>> RuleTypeOptions =>
        ruleTypes
            .Select(ruleType => new SelectOption<QualityCheckRuleType>(
                ruleType,
                L.Localize(ruleType)
            ))
            .ToList();

    protected override void OnInitialized()
    {
        (ruleTypes, parameterDescriptors) = operationRunner.Run(
            (QualityCheckCatalog catalog) =>
                (
                    catalog.RuleTypes,
                    (IReadOnlyDictionary<
                        QualityCheckRuleType,
                        IReadOnlyList<QualityCheckParameterDescriptor>
                    >)
                        catalog.RuleTypes.ToDictionary(ruleType => ruleType, catalog.GetParameters)
                )
        );

        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;

        rules = FormModel.Rules.Select(ToEditModel).ToList();
    }

    private QualityCheckRuleEditModel ToEditModel(QualityCheckRuleReadModel rule)
    {
        var values = QualityCheckParameterValues.Parse(rule.ParametersJson);
        var model = new QualityCheckRuleEditModel { RuleType = rule.RuleType };

        foreach (var descriptor in parameterDescriptors[rule.RuleType])
        {
            model.Parameters[descriptor.Key] = values.Read(descriptor);
        }

        return model;
    }

    private QualityCheckRuleEditModel CreateRule(QualityCheckRuleType ruleType)
    {
        var model = new QualityCheckRuleEditModel { RuleType = ruleType };

        foreach (var descriptor in parameterDescriptors[ruleType])
        {
            model.Parameters[descriptor.Key] = descriptor.DefaultValue;
        }

        return model;
    }

    private void AddRule()
    {
        rules.Add(CreateRule(ruleTypes[0]));
    }

    private void RemoveRule(QualityCheckRuleEditModel rule)
    {
        rules.Remove(rule);
    }

    private void ChangeRuleType(QualityCheckRuleEditModel rule, QualityCheckRuleType ruleType)
    {
        rule.RuleType = ruleType;
        rule.Parameters.Clear();

        foreach (var descriptor in parameterDescriptors[ruleType])
        {
            rule.Parameters[descriptor.Key] = descriptor.DefaultValue;
        }
    }

    private string? HelperText(QualityCheckParameterDescriptor descriptor) =>
        descriptor.HelperTextKey is null ? null : L[descriptor.HelperTextKey].Value;

    private static string GetText(QualityCheckRuleEditModel rule, string key) =>
        rule.Parameters.GetValueOrDefault(key) as string ?? string.Empty;

    private static void SetText(QualityCheckRuleEditModel rule, string key, string value) =>
        rule.Parameters[key] = value;

    private static int GetInt(QualityCheckRuleEditModel rule, string key) =>
        rule.Parameters.GetValueOrDefault(key) is int value ? value : 0;

    private static void SetInt(QualityCheckRuleEditModel rule, string key, int value) =>
        rule.Parameters[key] = value;

    private static bool GetBool(QualityCheckRuleEditModel rule, string key) =>
        rule.Parameters.GetValueOrDefault(key) is true;

    private static void SetBool(QualityCheckRuleEditModel rule, string key, bool value) =>
        rule.Parameters[key] = value;

    private async Task SaveAsync()
    {
        var inputs = rules
            .Select(rule => new QualityCheckRuleInput(
                rule.RuleType,
                QualityCheckParameterValues.Serialize(rule.Parameters)
            ))
            .ToList();

        if (FormModel is { IsEdit: true, QualityProfileId: not null })
        {
            await operationRunner.RunAsync(
                (QualityProfileService service) =>
                    service.UpdateAsync(FormModel.QualityProfileId.Value, FormModel.Name, inputs)
            );
            await DialogRef.CloseAsync(DialogResult.Ok(FormModel.QualityProfileId.Value));
            return;
        }

        var id = await operationRunner.RunAsync(
            (QualityProfileService service) => service.CreateAsync(FormModel.Name, inputs)
        );
        await DialogRef.CloseAsync(DialogResult.Ok(id));
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name, L["NameIsRequired"]);
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
