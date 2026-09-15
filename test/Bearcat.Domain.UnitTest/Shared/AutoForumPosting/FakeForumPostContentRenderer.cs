using Bearcat.Domain.Shared.AutoForumPosting;
using Bearcat.Domain.Shared.ForumPostRendering;

namespace Bearcat.Domain.UnitTest.Shared.AutoForumPosting;

public sealed class FakeForumPostContentRenderer : IForumPostContentRenderer
{
    public string Content { get; init; } = "Rendered body";

    public List<string> Errors { get; init; } = [];

    public List<int> RenderedTemplateIds { get; } = [];

    public Task<ForumPostTemplateRenderResult> RenderAsync(
        int entityId,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        RenderedTemplateIds.Add(forumPostTemplateId);

        return Task.FromResult(new ForumPostTemplateRenderResult(Content, Errors));
    }
}
