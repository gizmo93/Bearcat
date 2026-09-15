using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class RuleConditionSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(RuleCondition condition)
    {
        return JsonSerializer.Serialize(condition, Options);
    }

    public static bool TryDeserialize(string? json, out RuleCondition? condition)
    {
        condition = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            condition = JsonSerializer.Deserialize<RuleCondition>(json, Options);
        }
        catch (JsonException)
        {
            return false;
        }

        return condition is not null;
    }

    public static RuleCondition Deserialize(string? json)
    {
        if (!TryDeserialize(json, out var condition) || condition is null)
        {
            throw new RuleConditionFormatException("The rule condition is not valid JSON.");
        }

        return condition;
    }

    public static List<string> Validate(
        RuleCondition? condition,
        IReadOnlyList<RuleFieldDescriptor> fields
    )
    {
        var errors = new List<string>();

        if (condition is null)
        {
            errors.Add("A rule condition is required.");

            return errors;
        }

        Validate(condition, fields, errors);

        return errors;
    }

    public static List<string> ValidateJson(string? json, IReadOnlyList<RuleFieldDescriptor> fields)
    {
        if (!TryDeserialize(json, out var condition))
        {
            return ["The rule condition is not valid JSON."];
        }

        return Validate(condition, fields);
    }

    private static void Validate(
        RuleCondition condition,
        IReadOnlyList<RuleFieldDescriptor> fields,
        List<string> errors
    )
    {
        switch (condition.Kind)
        {
            case RuleConditionKind.All:
            case RuleConditionKind.Any:
                ValidateGroup(condition, fields, errors);

                break;
            case RuleConditionKind.Not:
                ValidateNot(condition, fields, errors);

                break;
            case RuleConditionKind.Comparison:
                ValidateComparison(condition, fields, errors);

                break;
            default:
                errors.Add($"Unknown condition kind '{condition.Kind}'.");

                break;
        }
    }

    private static void ValidateGroup(
        RuleCondition condition,
        IReadOnlyList<RuleFieldDescriptor> fields,
        List<string> errors
    )
    {
        var kind = KindName(condition.Kind);

        if (condition.Children is null || condition.Children.Count == 0)
        {
            errors.Add($"The '{kind}' group requires at least one child condition.");
        }

        if (condition.Child is not null)
        {
            errors.Add($"The '{kind}' group must use 'children' instead of 'child'.");
        }

        if (condition.Field is not null || condition.Operator is not null)
        {
            errors.Add($"The '{kind}' group must not define a field or an operator.");
        }

        foreach (var child in condition.Children ?? [])
        {
            Validate(child, fields, errors);
        }
    }

    private static void ValidateNot(
        RuleCondition condition,
        IReadOnlyList<RuleFieldDescriptor> fields,
        List<string> errors
    )
    {
        if (condition.Children is { Count: > 0 })
        {
            errors.Add("The 'not' condition must use 'child' instead of 'children'.");
        }

        if (condition.Child is null)
        {
            errors.Add("The 'not' condition requires a child condition.");

            return;
        }

        Validate(condition.Child, fields, errors);
    }

    private static void ValidateComparison(
        RuleCondition condition,
        IReadOnlyList<RuleFieldDescriptor> fields,
        List<string> errors
    )
    {
        if (condition.Children is { Count: > 0 } || condition.Child is not null)
        {
            errors.Add("A comparison condition must not contain child conditions.");
        }

        if (string.IsNullOrWhiteSpace(condition.Field))
        {
            errors.Add("A comparison condition requires a field.");

            return;
        }

        var descriptor = fields.FirstOrDefault(field =>
            string.Equals(field.Name, condition.Field, StringComparison.OrdinalIgnoreCase)
        );

        if (descriptor is null)
        {
            errors.Add($"Unknown field '{condition.Field}'.");

            return;
        }

        if (condition.Operator is null)
        {
            errors.Add($"The field '{descriptor.Name}' requires an operator.");

            return;
        }

        var conditionOperator = condition.Operator.Value;

        if (!descriptor.AllowedOperators.Contains(conditionOperator))
        {
            errors.Add(
                $"The operator '{conditionOperator}' is not allowed for the field '{descriptor.Name}'."
            );

            return;
        }

        ValidateOperands(condition, descriptor, conditionOperator, errors);
    }

    private static void ValidateOperands(
        RuleCondition condition,
        RuleFieldDescriptor descriptor,
        RuleConditionOperator conditionOperator,
        List<string> errors
    )
    {
        if (conditionOperator is RuleConditionOperator.IsSet or RuleConditionOperator.IsNotSet)
        {
            return;
        }

        if (conditionOperator is RuleConditionOperator.In or RuleConditionOperator.NotIn)
        {
            if (condition.Values is null || condition.Values.Count == 0)
            {
                errors.Add(
                    $"The operator '{conditionOperator}' requires at least one value for the field '{descriptor.Name}'."
                );

                return;
            }

            foreach (var value in condition.Values)
            {
                ValidateValue(value, descriptor, errors);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(condition.Value))
        {
            errors.Add(
                $"The operator '{conditionOperator}' requires a value for the field '{descriptor.Name}'."
            );

            return;
        }

        ValidateValue(condition.Value, descriptor, errors);
    }

    private static void ValidateValue(
        string? value,
        RuleFieldDescriptor descriptor,
        List<string> errors
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"The field '{descriptor.Name}' requires a value.");

            return;
        }

        switch (descriptor.ValueKind)
        {
            case RuleFieldValueKind.Enumeration
                when !descriptor.Options.Contains(value, StringComparer.OrdinalIgnoreCase):
                errors.Add($"The value '{value}' is not valid for the field '{descriptor.Name}'.");

                break;
            case RuleFieldValueKind.Integer
                when !int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _
                ):
                errors.Add(
                    $"The value '{value}' is not a number for the field '{descriptor.Name}'."
                );

                break;
            case RuleFieldValueKind.Boolean when !bool.TryParse(value, out _):
                errors.Add(
                    $"The value '{value}' is not a boolean for the field '{descriptor.Name}'."
                );

                break;
        }
    }

    private static string KindName(RuleConditionKind kind)
    {
        return kind.ToString().ToLowerInvariant();
    }
}
