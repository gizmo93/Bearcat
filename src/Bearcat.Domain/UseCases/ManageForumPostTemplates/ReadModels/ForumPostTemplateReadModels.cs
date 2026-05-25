namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;

public record ForumPostTemplateSummaryReadModel(
    int ForumPostTemplateId,
    string Name,
    DateTime UpdatedAt
);

public record ForumPostTemplateDetailReadModel(
    int ForumPostTemplateId,
    string Name,
    string TemplateBody
);

public record ForumPostTemplateValidationResult(bool IsValid, IReadOnlyList<string> Errors);
