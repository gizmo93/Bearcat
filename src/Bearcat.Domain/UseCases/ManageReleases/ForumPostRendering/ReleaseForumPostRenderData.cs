using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

public record ReleaseForumPostRenderData(
    ForumPostTemplateRenderModel RenderModel,
    ScriptObject ImageLinks
);
