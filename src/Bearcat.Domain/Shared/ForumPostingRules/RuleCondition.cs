namespace Bearcat.Domain.Shared.ForumPostingRules;

public sealed class RuleCondition
{
    public RuleConditionKind Kind { get; set; }

    public List<RuleCondition> Children { get; set; } = [];

    public RuleCondition? Child { get; set; }

    public string Field { get; set; } = string.Empty;

    public RuleConditionOperator Operator { get; set; } = RuleConditionOperator.Equals;

    public string Value { get; set; } = string.Empty;

    public List<string> Values { get; set; } = [];
}
