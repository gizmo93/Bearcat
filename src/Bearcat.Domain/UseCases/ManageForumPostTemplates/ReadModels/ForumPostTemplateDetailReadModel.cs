using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;

public record ForumPostTemplateDetailReadModel(
    int ForumPostTemplateId,
    string Name,
    ForumPostTemplateType Type,
    ForumPostTemplateOutputFormat OutputFormat,
    string TemplateBody
);
