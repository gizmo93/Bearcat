using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.ValueObjects;
using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

public class ReleaseForumPostRenderSource(ReleaseForumPostRenderModelBuilder renderModelBuilder)
    : IForumPostRenderSource
{
    public ForumPostTemplateType Type => ForumPostTemplateType.Release;

    public IReadOnlyList<ForumPostTemplateVariableNode> GetVariables()
    {
        return
        [
            .. ForumPostTemplateVariableCatalog.GetVariables(typeof(ForumPostTemplateRenderModel)),
            ForumPostImageLinkBuilder.CreateVariableNode("imagelinks"),
        ];
    }

    public async Task<ScriptObject?> BuildGlobalsAsync(
        int entityId,
        CancellationToken cancellationToken = default
    )
    {
        var renderDataByReleaseId = await renderModelBuilder.BuildAsync(
            [entityId],
            cancellationToken
        );

        if (!renderDataByReleaseId.TryGetValue(entityId, out var renderData))
        {
            return null;
        }

        var scriptObject = new ScriptObject();

        scriptObject.Import(
            renderData.RenderModel,
            ForumPostTemplateVariableCatalog.ShouldExposeMember
        );

        scriptObject["imagelinks"] = renderData.ImageLinks;

        return scriptObject;
    }
}
