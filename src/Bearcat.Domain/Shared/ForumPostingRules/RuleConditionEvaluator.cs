using System.Text.RegularExpressions;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class RuleConditionEvaluator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static bool IsMatch(RuleCondition condition, ReleaseRoutingContext context)
    {
        return condition.Kind switch
        {
            RuleConditionKind.All => condition.Children!.TrueForAll(child =>
                IsMatch(child, context)
            ),
            RuleConditionKind.Any => condition.Children!.Exists(child => IsMatch(child, context)),
            RuleConditionKind.Not => !IsMatch(condition.Child!, context),
            RuleConditionKind.Comparison => IsComparisonMatch(condition, context),
            _ => throw new InvalidOperationException($"Unknown condition kind '{condition.Kind}'."),
        };
    }

    private static bool IsComparisonMatch(RuleCondition condition, ReleaseRoutingContext context)
    {
        var field = RuleFieldCatalog.Get(condition.Field!);
        var actual = field.GetValue(context);

        return condition.Operator switch
        {
            RuleConditionOperator.IsSet => IsSet(actual),
            RuleConditionOperator.IsNotSet => !IsSet(actual),
            RuleConditionOperator.Equals => IsEqual(field, actual, condition.Value!),
            RuleConditionOperator.NotEquals => !IsEqual(field, actual, condition.Value!),
            RuleConditionOperator.In => IsOneOf(field, actual, condition.Values!),
            RuleConditionOperator.NotIn => !IsOneOf(field, actual, condition.Values!),
            RuleConditionOperator.Like => IsLike(actual, condition.Value!),
            RuleConditionOperator.NotLike => !IsLike(actual, condition.Value!),
            RuleConditionOperator.Regex => IsRegexMatch(actual, condition.Value!),
            RuleConditionOperator.GreaterOrEqual => actual is not null
                && actual.CompareTo(field.ParseValue(condition.Value!)) >= 0,
            RuleConditionOperator.LessOrEqual => actual is not null
                && actual.CompareTo(field.ParseValue(condition.Value!)) <= 0,
            _ => throw new InvalidOperationException(
                $"Unknown condition operator '{condition.Operator}'."
            ),
        };
    }

    private static bool IsSet(IComparable? actual)
    {
        if (actual is string text)
        {
            return !string.IsNullOrWhiteSpace(text);
        }

        return actual is not null;
    }

    private static bool IsEqual(RuleFieldDescriptor field, IComparable? actual, string value)
    {
        if (actual is string text)
        {
            return string.Equals(text, value, StringComparison.OrdinalIgnoreCase);
        }

        return actual is not null && actual.CompareTo(field.ParseValue(value)) == 0;
    }

    private static bool IsOneOf(RuleFieldDescriptor field, IComparable? actual, List<string> values)
    {
        return values.Exists(value => IsEqual(field, actual, value));
    }

    private static bool IsLike(IComparable? actual, string likePattern)
    {
        var segments = likePattern.Split('%').Select(Regex.Escape);

        return IsRegexMatch(actual, $"^{string.Join(".*", segments)}$");
    }

    private static bool IsRegexMatch(IComparable? actual, string pattern)
    {
        if (actual is not string text)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
