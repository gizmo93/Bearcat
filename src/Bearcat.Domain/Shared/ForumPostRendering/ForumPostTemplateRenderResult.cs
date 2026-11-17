namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ForumPostTemplateRenderResult(
    string Content,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> BlockedReasons
)
{
    public bool IsSuccess => Errors.Count == 0;
}
