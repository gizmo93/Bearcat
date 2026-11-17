using Bearcat.Domain.Shared.ForumPostRendering;

namespace Bearcat.Domain.Shared.ForumPosting;

public interface IForumPostContentRenderer
{
    Task<ForumPostTemplateRenderResult> RenderAsync(
        int entityId,
        int forumPostTemplateId,
        int? distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    );
}
