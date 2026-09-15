using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;

public record ForumPostingRuleSummaryReadModel(
    int ForumPostingRuleId,
    int DistributionSiteRegistrationId,
    int SortOrder,
    string Name,
    string TargetNodeId,
    string TargetPathSnapshot,
    string? ThreadPrefixId,
    int ForumPostTemplateId,
    string ForumPostTemplateName,
    ForumPostPostMode PostMode,
    bool IsEnabled,
    DateTime UpdatedAt
);

public record ForumPostingRulePreviewReadModel(
    int ReleaseId,
    string ReleaseName,
    string? MatchedRuleName,
    string? TargetPathSnapshot
);

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
    bool IsEnabled
);
