using Bearcat.Abstractions.ConfigurationFields;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.Extensions.Localization;

namespace Bearcat.Website.Pages.ManageRemoteSources;

public static class ConfigurationFieldFormMapper
{
    public static FormFieldDefinition ToFormField(
        ConfigurationField field,
        IStringLocalizer localizer,
        string resourcePrefix,
        bool isEdit
    )
    {
        var keepsExistingSecret = isEdit && field.Type == ConfigurationFieldType.Password;
        var resourceKey = $"{resourcePrefix}{field.Key}";

        return new FormFieldDefinition
        {
            Name = field.Key,
            Label = LocalizeOrDefault(localizer, resourceKey) ?? field.Key,
            Description = keepsExistingSecret
                ? localizer["SecretKeepCurrentHelp"].Value
                : LocalizeOrDefault(localizer, $"{resourceKey}_Description"),
            Placeholder = keepsExistingSecret
                ? localizer["SecretUnchangedPlaceholder"].Value
                : null,
            Type = ToFieldType(field.Type),
            Required = field.IsRequired && !keepsExistingSecret,
            DefaultValue = isEdit ? null : field.DefaultValue,
            Options = field
                .Options?.Select(option => new SelectOption<string>(
                    option,
                    LocalizeOrDefault(localizer, $"{resourceKey}_{option}") ?? option
                ))
                .ToList(),
            Validations =
                field.Type == ConfigurationFieldType.Number
                    ? [new FieldValidation { Type = ValidationType.Custom }]
                    : null,
            Metadata =
                field.Type == ConfigurationFieldType.Number
                    ? CreateWholeNumberMetadata(localizer["MustBeWholeNumber"].Value)
                    : null,
        };
    }

    public static Dictionary<string, object> CreateWholeNumberMetadata(
        string errorMessage,
        int? minimum = null
    )
    {
        var metadata = new Dictionary<string, object>
        {
            ["step"] = 1,
            ["customValidator"] =
                (Func<object?, string?>)(value => IsWholeNumber(value) ? null : errorMessage),
        };

        if (minimum is not null)
        {
            metadata["min"] = minimum.Value;
        }

        return metadata;
    }

    private static bool IsWholeNumber(object? value)
    {
        return value switch
        {
            null => true,
            int or long => true,
            double number => double.IsInteger(number),
            decimal number => decimal.IsInteger(number),
            _ => false,
        };
    }

    private static FieldType ToFieldType(ConfigurationFieldType type)
    {
        return type switch
        {
            ConfigurationFieldType.Text => FieldType.Text,
            ConfigurationFieldType.Password => FieldType.Password,
            ConfigurationFieldType.Number => FieldType.Number,
            ConfigurationFieldType.Boolean => FieldType.Switch,
            ConfigurationFieldType.Select => FieldType.Select,
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Unknown configuration field type"
            ),
        };
    }

    private static string? LocalizeOrDefault(IStringLocalizer localizer, string key)
    {
        var localized = localizer[key];

        return localized.ResourceNotFound ? null : localized.Value;
    }
}
