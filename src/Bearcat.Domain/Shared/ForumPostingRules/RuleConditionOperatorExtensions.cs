namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class RuleConditionOperatorExtensions
{
    public static bool IsMultiValue(this RuleConditionOperator conditionOperator)
    {
        return conditionOperator is RuleConditionOperator.In or RuleConditionOperator.NotIn;
    }

    public static bool NeedsValue(this RuleConditionOperator conditionOperator)
    {
        return conditionOperator
            is not (RuleConditionOperator.IsSet or RuleConditionOperator.IsNotSet);
    }
}
