using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;

public record ForumPostTemplateSummaryReadModel(
    int ForumPostTemplateId,
    string Name,
    ForumPostTemplateType Type,
    ForumPostTemplateOutputFormat OutputFormat,
    DateTime UpdatedAt,
    int ForumPostingRuleCount
);
