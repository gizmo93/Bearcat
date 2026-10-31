using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostingRules;

public class ForumPostingRulePreviewService(IForumPostingRuleReadRepository readRepository)
{
    public const int DefaultEntityCount = 50;

    public async Task<IReadOnlyList<ForumPostingRulePreviewReadModel>> PreviewAsync(
        int distributionSiteRegistrationId,
        ForumPostTemplateType entityType,
        int entityCount = DefaultEntityCount,
        CancellationToken cancellationToken = default
    )
    {
        var rules = await readRepository.GetRulesForMatchingAsync(
            distributionSiteRegistrationId,
            cancellationToken
        );

        if (entityType == ForumPostTemplateType.Release)
        {
            var releases = await readRepository.GetRecentReleasesForPreviewAsync(
                entityCount,
                cancellationToken
            );

            return releases
                .Select(release =>
                    CreatePreview(
                        rules: rules,
                        entityType: entityType,
                        entityId: release.Id,
                        entityName: release.Name,
                        context: ReleaseRoutingContext.FromRelease(release)
                    )
                )
                .ToList();
        }

        var collections = await readRepository.GetRecentReleaseCollectionsForPreviewAsync(
            entityCount,
            cancellationToken
        );

        return collections
            .Select(collection =>
                CreatePreview(
                    rules: rules,
                    entityType: entityType,
                    entityId: collection.Id,
                    entityName: collection.Name,
                    context: ReleaseRoutingContext.FromReleaseCollection(collection)
                )
            )
            .ToList();
    }

    private static ForumPostingRulePreviewReadModel CreatePreview(
        IReadOnlyList<ForumPostingRule> rules,
        ForumPostTemplateType entityType,
        int entityId,
        string entityName,
        ReleaseRoutingContext context
    )
    {
        var match = ForumPostingRuleMatcher.FindFirstMatch(
            rules: rules,
            entityType: entityType,
            context: context
        );

        return new ForumPostingRulePreviewReadModel(
            EntityId: entityId,
            EntityName: entityName,
            MatchedRuleName: match?.Name,
            TargetPathSnapshot: match?.TargetPathSnapshot
        );
    }
}
