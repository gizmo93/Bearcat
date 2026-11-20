using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostingRules.Dto;

public record ForumPostingRuleInput(
    string Name,
    string ConditionJson,
    string TargetNodeId,
    string TargetPathSnapshot,
    IReadOnlyList<string> ThreadPrefixIds,
    int ForumPostTemplateId,
    ForumPostPostMode PostMode,
    bool IsEnabled
);
