using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.AutoForumPosting;

public sealed record AutoPostRuleMatch(
    int ForumPostingRuleId,
    string RuleName,
    string TargetNodeId,
    string TargetPathSnapshot,
    string? ThreadPrefixId,
    int ForumPostTemplateId,
    ForumPostPostMode PostMode
);
