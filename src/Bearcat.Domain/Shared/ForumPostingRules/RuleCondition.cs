namespace Bearcat.Domain.Shared.ForumPostingRules;

public sealed class RuleCondition
{
    public RuleConditionKind Kind { get; set; }

    public List<RuleCondition>? Children { get; set; }

    public RuleCondition? Child { get; set; }

    public string? Field { get; set; }

    public RuleConditionOperator? Operator { get; set; }

    public string? Value { get; set; }

    public List<string>? Values { get; set; }

    public static RuleCondition All(params RuleCondition[] children)
    {
        return new RuleCondition { Kind = RuleConditionKind.All, Children = [.. children] };
    }

    public static RuleCondition Any(params RuleCondition[] children)
    {
        return new RuleCondition { Kind = RuleConditionKind.Any, Children = [.. children] };
    }

    public static RuleCondition Not(RuleCondition child)
    {
        return new RuleCondition { Kind = RuleConditionKind.Not, Child = child };
    }

    public static RuleCondition Compare(
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

    public static RuleCondition CompareMany(
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
            Values = [.. values],
        };
    }
}
