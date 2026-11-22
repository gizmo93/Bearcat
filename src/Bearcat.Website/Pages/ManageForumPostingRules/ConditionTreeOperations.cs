using Bearcat.Domain.Shared.ForumPostingRules;

namespace Bearcat.Website.Pages.ManageForumPostingRules;

public static class ConditionTreeOperations
{
    public static RuleCondition CreateGroup()
    {
        return new RuleCondition { Kind = RuleConditionKind.All, Children = [CreateComparison()] };
    }

    public static RuleCondition CreateNot()
    {
        return new RuleCondition { Kind = RuleConditionKind.Not, Child = CreateComparison() };
    }

    public static RuleCondition CreateComparison()
    {
        var descriptor = RuleFieldCatalog.Fields[0];

        return new RuleCondition
        {
            Kind = RuleConditionKind.Comparison,
            Field = descriptor.Name,
            Operator = descriptor.AllowedOperators[0],
        };
    }

    public static void ChangeField(RuleCondition comparison, string fieldName)
    {
        var descriptor = RuleFieldCatalog.Get(fieldName);

        comparison.Field = fieldName;
        comparison.Value = string.Empty;
        comparison.Values = [];

        if (!descriptor.AllowedOperators.Contains(comparison.Operator))
        {
            comparison.Operator = descriptor.AllowedOperators[0];
        }
    }

    public static void ChangeOperator(
        RuleCondition comparison,
        RuleConditionOperator conditionOperator
    )
    {
        var wasMultiValue = comparison.Operator.IsMultiValue();
        comparison.Operator = conditionOperator;

        if (wasMultiValue == conditionOperator.IsMultiValue())
        {
            return;
        }

        comparison.Value = string.Empty;
        comparison.Values = [];
    }

    public static void RemoveComparisonsWithUnsupportedFields(
        RuleCondition condition,
        IReadOnlyList<RuleFieldDescriptor> supportedFields
    )
    {
        condition.Children.RemoveAll(child => !IsSupported(child, supportedFields));

        foreach (var child in condition.Children)
        {
            RemoveComparisonsWithUnsupportedFields(child, supportedFields);
        }

        if (condition.Child is null)
        {
            return;
        }

        if (!IsSupported(condition.Child, supportedFields))
        {
            condition.Child = null;

            return;
        }

        RemoveComparisonsWithUnsupportedFields(condition.Child, supportedFields);
    }

    private static bool IsSupported(
        RuleCondition condition,
        IReadOnlyList<RuleFieldDescriptor> supportedFields
    )
    {
        return condition.Kind is not RuleConditionKind.Comparison
            || supportedFields.Any(field =>
                string.Equals(field.Name, condition.Field, StringComparison.OrdinalIgnoreCase)
            );
    }
}
