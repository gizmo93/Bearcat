namespace Bearcat.Domain.Shared.ForumPostingRules;

public sealed record RuleFieldDescriptor(
    string Name,
    RuleFieldValueKind ValueKind,
    IReadOnlyList<RuleConditionOperator> AllowedOperators,
    IReadOnlyList<string> Options,
    Func<ReleaseRoutingContext, object?> ValueAccessor
);
