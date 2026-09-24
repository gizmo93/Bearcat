using System.Globalization;
using Bearcat.Abstractions.ConfigurationFields;

namespace Bearcat.Domain.Shared.ConfigurationFields;

public static class ConfigurationValueNormalizer
{
    public static Dictionary<string, object?> Normalize(
        IReadOnlyList<ConfigurationField> fields,
        IReadOnlyDictionary<string, object?> submittedValues,
        IReadOnlyDictionary<string, object?>? existingValues = null
    )
    {
        var fieldsByKey = fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
        var result = new Dictionary<string, object?>(
            existingValues ?? new Dictionary<string, object?>(),
            StringComparer.Ordinal
        );

        foreach (var (key, submittedValue) in submittedValues)
        {
            if (!fieldsByKey.TryGetValue(key, out var field))
            {
                throw new ConfigurationFieldValidationException(
                    key,
                    ConfigurationFieldValidationError.UnknownField
                );
            }

            var value = NormalizeValue(field, submittedValue);

            if (value is not null)
            {
                result[key] = value;
            }
            else if (field.Type != ConfigurationFieldType.Password)
            {
                result.Remove(key);
            }
        }

        var missingField = fields.FirstOrDefault(field =>
            field.IsRequired && result.GetValueOrDefault(field.Key) is null
        );

        if (missingField is not null)
        {
            throw new ConfigurationFieldValidationException(
                missingField.Key,
                ConfigurationFieldValidationError.Required
            );
        }

        return result;
    }

    private static object? NormalizeValue(ConfigurationField field, object? value)
    {
        if (value is null)
        {
            return null;
        }

        return field.Type switch
        {
            ConfigurationFieldType.Text => NormalizeText(field, value),
            ConfigurationFieldType.Password => NormalizePassword(field, value),
            ConfigurationFieldType.Number => NormalizeNumber(field, value),
            ConfigurationFieldType.Boolean => NormalizeBoolean(field, value),
            ConfigurationFieldType.Select => NormalizeSelect(field, value),
            _ => throw new ArgumentOutOfRangeException(
                nameof(field),
                field.Type,
                "Unknown configuration field type"
            ),
        };
    }

    private static string? NormalizeText(ConfigurationField field, object value)
    {
        var text = RequireString(field, value).Trim();

        return text.Length == 0 ? null : text;
    }

    private static string? NormalizePassword(ConfigurationField field, object value)
    {
        var password = RequireString(field, value);

        return password.Length == 0 ? null : password;
    }

    private static string? NormalizeSelect(ConfigurationField field, object value)
    {
        var option = NormalizeText(field, value);

        if (option is not null && field.Options?.Contains(option, StringComparer.Ordinal) != true)
        {
            throw new ConfigurationFieldValidationException(
                field.Key,
                ConfigurationFieldValidationError.InvalidOption
            );
        }

        return option;
    }

    private static int? NormalizeNumber(ConfigurationField field, object value)
    {
        if (value is string blank && string.IsNullOrWhiteSpace(blank))
        {
            return null;
        }

        var number = value switch
        {
            int integer => integer,
            long integer => integer,
            double real => real,
            float real => real,
            decimal real => (double)real,
            string text
                when double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed
                ) => parsed,
            _ => double.NaN,
        };

        if (!double.IsInteger(number) || number < int.MinValue || number > int.MaxValue)
        {
            throw new ConfigurationFieldValidationException(
                field.Key,
                ConfigurationFieldValidationError.NotAnInteger
            );
        }

        return (int)number;
    }

    private static bool? NormalizeBoolean(ConfigurationField field, object value)
    {
        return value switch
        {
            bool boolean => boolean,
            string text when string.IsNullOrWhiteSpace(text) => null,
            string text when bool.TryParse(text.Trim(), out var parsed) => parsed,
            _ => throw new ConfigurationFieldValidationException(
                field.Key,
                ConfigurationFieldValidationError.InvalidValue
            ),
        };
    }

    private static string RequireString(ConfigurationField field, object value)
    {
        return value as string
            ?? throw new ConfigurationFieldValidationException(
                field.Key,
                ConfigurationFieldValidationError.InvalidValue
            );
    }
}
