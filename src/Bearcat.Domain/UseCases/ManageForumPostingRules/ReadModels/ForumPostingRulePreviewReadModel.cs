namespace Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;

public record ForumPostingRulePreviewReadModel(
    int EntityId,
    string EntityName,
    string? MatchedRuleName,
    string? TargetPathSnapshot
);
