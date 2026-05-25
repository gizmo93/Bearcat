namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ForumPostTemplateVariableReadModel(string Path, string Description);

public record ForumPostTemplateRenderResult(string Content, IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Errors.Count == 0;
}
