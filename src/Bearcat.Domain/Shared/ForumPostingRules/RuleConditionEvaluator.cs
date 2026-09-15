using System.Globalization;
using System.Text.RegularExpressions;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class RuleConditionEvaluator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static bool IsMatch(RuleCondition? condition, ReleaseRoutingContext context)
    {
        if (condition is null)
        {
            return false;
        }

        return condition.Kind switch
        {
            RuleConditionKind.All => condition.Children is { Count: > 0 }
                && condition.Children.TrueForAll(child => IsMatch(child, context)),
            RuleConditionKind.Any => condition.Children is { Count: > 0 }
                && condition.Children.Exists(child => IsMatch(child, context)),
            RuleConditionKind.Not => condition.Child is not null
                && !IsMatch(condition.Child, context),
            RuleConditionKind.Comparison => IsComparisonMatch(condition, context),
            _ => false,
        };
    }

    private static bool IsComparisonMatch(RuleCondition condition, ReleaseRoutingContext context)
    {
        if (!RuleFieldCatalog.TryGet(condition.Field, out var descriptor))
        {
            return false;
        }

        if (condition.Operator is not { } conditionOperator)
        {
            return false;
        }

        if (!descriptor.AllowedOperators.Contains(conditionOperator))
        {
            return false;
        }

        var actual = descriptor.ValueAccessor(context);

        return conditionOperator switch
        {
            RuleConditionOperator.IsSet => IsSet(actual),
            RuleConditionOperator.IsNotSet => !IsSet(actual),
            RuleConditionOperator.NotEquals => !IsPositiveMatch(
                RuleConditionOperator.Equals,
                condition,
                descriptor,
                actual
            ),
            RuleConditionOperator.NotIn => !IsPositiveMatch(
                RuleConditionOperator.In,
                condition,
                descriptor,
                actual
            ),
            RuleConditionOperator.NotLike => !IsPositiveMatch(
                RuleConditionOperator.Like,
                condition,
                descriptor,
                actual
            ),
            _ => IsPositiveMatch(conditionOperator, condition, descriptor, actual),
        };
    }

    private static bool IsPositiveMatch(
        RuleConditionOperator conditionOperator,
        RuleCondition condition,
        RuleFieldDescriptor descriptor,
        object? actual
    )
    {
        if (actual is null)
        {
            return false;
        }

        if (conditionOperator is RuleConditionOperator.In)
        {
            return condition.Values is { Count: > 0 }
                && condition.Values.Exists(value =>
                    IsPositiveMatch(RuleConditionOperator.Equals, descriptor, actual, value)
                );
        }

        return IsPositiveMatch(conditionOperator, descriptor, actual, condition.Value);
    }

    private static bool IsPositiveMatch(
        RuleConditionOperator conditionOperator,
        RuleFieldDescriptor descriptor,
        object actual,
        string? value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return descriptor.ValueKind switch
        {
            RuleFieldValueKind.Text => IsTextMatch(conditionOperator, actual.ToString(), value),
            RuleFieldValueKind.Enumeration => IsEnumerationMatch(conditionOperator, actual, value),
            RuleFieldValueKind.Integer => IsIntegerMatch(conditionOperator, actual, value),
            RuleFieldValueKind.Boolean => IsBooleanMatch(conditionOperator, actual, value),
            _ => false,
        };
    }

    private static bool IsTextMatch(
        RuleConditionOperator conditionOperator,
        string? actual,
        string value
    )
    {
        if (actual is null)
        {
            return false;
        }

        return conditionOperator switch
        {
            RuleConditionOperator.Equals => string.Equals(
                actual,
                value,
                StringComparison.OrdinalIgnoreCase
            ),
            RuleConditionOperator.Like => IsRegexMatch(actual, ToLikePattern(value)),
            RuleConditionOperator.Regex => IsRegexMatch(actual, value),
            _ => false,
        };
    }

    private static bool IsEnumerationMatch(
        RuleConditionOperator conditionOperator,
        object actual,
        string value
    )
    {
        if (!Enum.TryParse(actual.GetType(), value, ignoreCase: true, out var parsed))
        {
            return false;
        }

        var actualNumber = Convert.ToInt64(actual, CultureInfo.InvariantCulture);
        var expectedNumber = Convert.ToInt64(parsed!, CultureInfo.InvariantCulture);

        return conditionOperator switch
        {
            RuleConditionOperator.Equals => actualNumber == expectedNumber,
            RuleConditionOperator.GreaterOrEqual => actualNumber >= expectedNumber,
            RuleConditionOperator.LessOrEqual => actualNumber <= expectedNumber,
            _ => false,
        };
    }

    private static bool IsIntegerMatch(
        RuleConditionOperator conditionOperator,
        object actual,
        string value
    )
    {
        if (
            actual is not int actualNumber
            || !int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var expectedNumber
            )
        )
        {
            return false;
        }

        return conditionOperator switch
        {
            RuleConditionOperator.Equals => actualNumber == expectedNumber,
            RuleConditionOperator.GreaterOrEqual => actualNumber >= expectedNumber,
            RuleConditionOperator.LessOrEqual => actualNumber <= expectedNumber,
            _ => false,
        };
    }

    private static bool IsBooleanMatch(
        RuleConditionOperator conditionOperator,
        object actual,
        string value
    )
    {
        if (actual is not bool actualFlag || !bool.TryParse(value, out var expectedFlag))
        {
            return false;
        }

        return conditionOperator == RuleConditionOperator.Equals && actualFlag == expectedFlag;
    }

    private static bool IsSet(object? actual)
    {
        return actual switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true,
        };
    }

    private static bool IsRegexMatch(string actual, string pattern)
    {
        try
        {
            return Regex.IsMatch(actual, pattern, RegexOptions.IgnoreCase, RegexTimeout);
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

    private static string ToLikePattern(string value)
    {
        var segments = value.Split('%').Select(Regex.Escape);

        return $"^{string.Join(".*", segments)}$";
    }
}
