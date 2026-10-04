using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;

namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;

public record ForumPostTemplatePreviewResult(
    string Content,
    IReadOnlyList<ForumPostTemplateError> Errors
);
