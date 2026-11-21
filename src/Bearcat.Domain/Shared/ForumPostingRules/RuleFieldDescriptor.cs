namespace Bearcat.Domain.Shared.ForumPostingRules;

public sealed record RuleFieldDescriptor(
    string Name,
    RuleFieldValueKind ValueKind,
    IReadOnlyList<RuleConditionOperator> AllowedOperators,
    IReadOnlyList<string> Options,
    Func<RuleFieldValues, IComparable?> GetValue,
    Func<string, IComparable> ParseValue,
    bool IsReleaseOnly = false
);
