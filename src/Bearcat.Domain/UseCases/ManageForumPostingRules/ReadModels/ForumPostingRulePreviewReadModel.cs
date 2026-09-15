namespace Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;

public record ForumPostingRulePreviewReadModel(
    int ReleaseId,
    string ReleaseName,
    string? MatchedRuleName,
    string? TargetPathSnapshot
);
