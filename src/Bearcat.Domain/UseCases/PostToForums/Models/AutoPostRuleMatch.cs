using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostRuleMatch(
    int ForumPostingRuleId,
    string RuleName,
    string TargetNodeId,
    string TargetPathSnapshot,
    string? ThreadPrefixId,
    int ForumPostTemplateId,
    ForumPostPostMode PostMode
);
