namespace Bearcat.Domain.Shared.ForumPostingRules;

public enum RuleConditionOperator
{
    Equals = 1,
    NotEquals = 2,
    In = 3,
    NotIn = 4,
    Like = 5,
    NotLike = 6,
    Regex = 7,
    GreaterOrEqual = 8,
    LessOrEqual = 9,
    IsSet = 10,
    IsNotSet = 11,
}
