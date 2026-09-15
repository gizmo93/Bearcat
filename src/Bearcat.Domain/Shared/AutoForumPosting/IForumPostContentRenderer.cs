using Bearcat.Domain.Shared.ForumPostRendering;

namespace Bearcat.Domain.Shared.AutoForumPosting;

public interface IForumPostContentRenderer
{
    Task<ForumPostTemplateRenderResult> RenderAsync(
        int entityId,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    );
}
