using Bearcat.Domain.Shared.ForumPostingRules;

namespace Bearcat.Website.Pages.ManageForumPostingRules;

public sealed class ConditionNodeModel
{
    public RuleConditionKind Kind { get; set; } = RuleConditionKind.All;

    public List<ConditionNodeModel> Children { get; set; } = [];

    public string Field { get; set; } = string.Empty;

    public RuleConditionOperator Operator { get; set; } = RuleConditionOperator.Equals;

    public string Value { get; set; } = string.Empty;

    public List<string> Values { get; set; } = [];

    public bool IsGroup => Kind is RuleConditionKind.All or RuleConditionKind.Any;
}
