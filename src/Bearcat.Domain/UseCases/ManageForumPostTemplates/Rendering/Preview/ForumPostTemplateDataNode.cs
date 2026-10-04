namespace Bearcat.Domain.UseCases.ManageForumPostTemplates.Rendering.Preview;

public record ForumPostTemplateDataNode(
    string Name,
    string? Value,
    IReadOnlyList<ForumPostTemplateDataNode> Children
);
