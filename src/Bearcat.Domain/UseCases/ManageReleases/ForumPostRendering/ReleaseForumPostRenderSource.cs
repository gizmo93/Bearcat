using Bearcat.Domain.Shared.ForumPosting;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.ValueObjects;
using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

public class ReleaseForumPostRenderSource(
    ReleaseForumPostRenderModelBuilder renderModelBuilder,
    IDistributionSiteRegistrationReadRepository distributionSiteRegistrationReadRepository
) : IForumPostRenderSource
{
    public ForumPostTemplateType Type => ForumPostTemplateType.Release;

    public IReadOnlyList<ForumPostTemplateVariableNode> GetVariables()
    {
        return
        [
            .. ForumPostTemplateVariableCatalog.GetVariables(typeof(ForumPostTemplateRenderModel)),
            ForumPostImageLinkBuilder.CreateReleaseVariableNode("imagelinks"),
        ];
    }

    public async Task<ForumPostRenderGlobals?> BuildGlobalsAsync(
        int entityId,
        int? distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var serviceLinksByReleaseId = await renderModelBuilder.LoadServiceLinksAsync(
            [entityId],
            cancellationToken
        );

        var serviceLinks = serviceLinksByReleaseId[entityId];

        var restrictions = distributionSiteRegistrationId is null
            ? DistributionSiteServiceRestrictions.Empty
            : await distributionSiteRegistrationReadRepository.GetServiceRestrictionsAsync(
                distributionSiteRegistrationId.Value,
                cancellationToken
            );

        var blockedReasons = restrictions.FindBlockedReasons(
            uploads: serviceLinks.Uploads,
            multiHosterLinkCrypters: serviceLinks.AllHostersLinkCrypters,
            imageLinks: serviceLinks.ImageLinks
        );

        var renderDataByReleaseId = await renderModelBuilder.BuildAsync(
            new Dictionary<int, ReleaseForumPostServiceLinks>
            {
                [entityId] = restrictions.RemoveForbiddenServiceLinks(serviceLinks),
            },
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

        return new ForumPostRenderGlobals(scriptObject, blockedReasons);
    }
}
