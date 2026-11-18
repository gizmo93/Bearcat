using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Domain.Shared.ConfigurationFields;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Bearcat.Website.Shared;

public static class ConfigurationFieldFormMapper
{
    public static FormFieldDefinition ToFormField(
        ConfigurationField field,
        IStringLocalizer localizer,
        string resourcePrefix,
        bool keepsStoredValues
    )
    {
        var keepsExistingSecret =
            keepsStoredValues && field.Type == ConfigurationFieldType.Password;
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
            DefaultValue = keepsStoredValues ? null : field.DefaultValue,
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

    public static Dictionary<
        string,
        RenderFragment<DynamicFieldRenderContext>
    > BuildFolderFieldRenderers(IReadOnlyList<ConfigurationField> fields, object eventReceiver)
    {
        return fields
            .Where(field => field.Type == ConfigurationFieldType.Folder)
            .ToDictionary(field => field.Key, _ => BuildFolderFieldRenderer(eventReceiver));
    }

    public static string CreateFieldErrorMessage(
        ConfigurationFieldValidationException exception,
        IStringLocalizer localizer,
        string resourcePrefix
    )
    {
        var fieldName =
            LocalizeOrDefault(localizer, $"{resourcePrefix}{exception.FieldKey}")
            ?? exception.FieldKey;

        return localizer[$"ConfigurationFieldError_{exception.Error}", fieldName].Value;
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

    private static RenderFragment<DynamicFieldRenderContext> BuildFolderFieldRenderer(
        object eventReceiver
    )
    {
        return context =>
            builder =>
            {
                builder.OpenComponent<FolderPathInput>(0);
                builder.AddComponentParameter(1, nameof(FolderPathInput.Value), context.Value);

                builder.AddComponentParameter(
                    2,
                    nameof(FolderPathInput.ValueChanged),
                    EventCallback.Factory.Create<string?>(
                        eventReceiver,
                        folderPath => context.ValueChanged(folderPath)
                    )
                );

                builder.AddComponentParameter(
                    3,
                    nameof(FolderPathInput.Disabled),
                    context.Disabled
                );

                builder.CloseComponent();
            };
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
            ConfigurationFieldType.Url => FieldType.Url,
            ConfigurationFieldType.Folder => FieldType.Custom,
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
