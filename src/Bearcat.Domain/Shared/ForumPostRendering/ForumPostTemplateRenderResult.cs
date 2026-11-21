namespace Bearcat.Domain.Shared.ForumPostRendering;

public record ForumPostTemplateRenderResult(
    string Content,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> BlockedReasons,
    string? CoverImageUrl
)
{
    public bool IsSuccess => Errors.Count == 0;
}
