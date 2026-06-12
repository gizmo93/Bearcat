using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;

public record ForumPostTemplateSummaryReadModel(
    int ForumPostTemplateId,
    string Name,
    ForumPostTemplateType Type,
    DateTime UpdatedAt
);

public record ForumPostTemplateDetailReadModel(
    int ForumPostTemplateId,
    string Name,
    ForumPostTemplateType Type,
    string TemplateBody
);

public record ForumPostTemplateValidationResult(bool IsValid, IReadOnlyList<string> Errors);
