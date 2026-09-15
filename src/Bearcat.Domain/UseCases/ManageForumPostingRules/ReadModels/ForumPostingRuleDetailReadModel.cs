using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;

public record ForumPostingRuleDetailReadModel(
    int ForumPostingRuleId,
    int DistributionSiteRegistrationId,
    int SortOrder,
    string Name,
    string ConditionJson,
    string TargetNodeId,
    string TargetPathSnapshot,
    string? ThreadPrefixId,
    int ForumPostTemplateId,
    string ForumPostTemplateName,
    ForumPostPostMode PostMode,
    bool StripDotsForThreadSearch,
    bool IsEnabled
);
