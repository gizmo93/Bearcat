using Bearcat.Domain.Shared.ForumPostingRules;

namespace Bearcat.Domain.UnitTest.Shared.ForumPostingRules;

public static class RuleConditionFactory
{
    public static RuleCondition CreateAll(params RuleCondition[] children)
    {
        return new RuleCondition { Kind = RuleConditionKind.All, Children = children.ToList() };
    }

    public static RuleCondition CreateAny(params RuleCondition[] children)
    {
        return new RuleCondition { Kind = RuleConditionKind.Any, Children = children.ToList() };
    }

    public static RuleCondition CreateNot(RuleCondition child)
    {
        return new RuleCondition { Kind = RuleConditionKind.Not, Child = child };
    }

    public static RuleCondition CreateComparison(
        string field,
        RuleConditionOperator conditionOperator,
        string? value
    )
    {
        return new RuleCondition
        {
            Kind = RuleConditionKind.Comparison,
            Field = field,
            Operator = conditionOperator,
            Value = value,
        };
    }

    public static RuleCondition CreateMultiValueComparison(
        string field,
        RuleConditionOperator conditionOperator,
        params string[] values
    )
    {
        return new RuleCondition
        {
            Kind = RuleConditionKind.Comparison,
            Field = field,
            Operator = conditionOperator,
            Values = values.ToList(),
        };
    }
}
