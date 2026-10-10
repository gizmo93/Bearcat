using System.Globalization;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Website.Formatting;
using Microsoft.Extensions.Localization;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;

public static class ConfigurationSettingFormatter
{
    public static bool IsSwitch(ApplicationConfigurationPropertyDto setting)
    {
        return setting.ValueType == typeof(bool);
    }

    public static bool HasOptions(ApplicationConfigurationPropertyDto setting)
    {
        return (setting.ValueType == typeof(string) || setting.ValueType.IsEnum)
            && setting.Options.Count > 0;
    }

    public static string? FormatOptionValue(object? value)
    {
        return value?.ToString();
    }

    public static object? ParseOptionValue(
        ApplicationConfigurationPropertyDto setting,
        string? option
    )
    {
        if (option is null || !setting.ValueType.IsEnum)
        {
            return option;
        }

        return Enum.Parse(setting.ValueType, option);
    }

    public static string? FormatEditorText(object? value)
    {
        return value switch
        {
            null => null,
            int intValue => intValue.ToString(CultureInfo.CurrentCulture),
            decimal decimalValue => DecimalInputConverter.FormatForInput(decimalValue),
            _ => throw new NotSupportedException(
                $"Configuration value type {value.GetType().Name} has no number editor."
            ),
        };
    }

    public static string FormatOptionLabel(
        IStringLocalizer<UiResource> localizer,
        ApplicationConfigurationPropertyDto setting,
        string option
    )
    {
        var resourceKeyPrefix = setting.ValueType.IsEnum
            ? setting.ValueType.Name
            : setting.DisplayName;
        var localizedOption = localizer[$"{resourceKeyPrefix}.{option}"];

        return localizedOption.ResourceNotFound ? option : localizedOption;
    }

    public static string FormatUnit(
        IStringLocalizer<UiResource> localizer,
        ApplicationConfigurationUnit unit
    )
    {
        return localizer[$"ConfigurationUnit.{unit}"];
    }

    public static string FormatValue(
        IStringLocalizer<UiResource> localizer,
        ApplicationConfigurationPropertyDto setting,
        object? value
    )
    {
        if (IsSwitch(setting))
        {
            return value is true ? localizer["Enabled"] : localizer["Disabled"];
        }

        if (HasOptions(setting))
        {
            return FormatOptionLabel(localizer, setting, FormatOptionValue(value)!);
        }

        var editorText = FormatEditorText(value);

        if (editorText is null)
        {
            return localizer["NoLimit"];
        }

        return setting.Unit is { } unit
            ? $"{editorText} {FormatUnit(localizer, unit)}"
            : editorText;
    }
}
