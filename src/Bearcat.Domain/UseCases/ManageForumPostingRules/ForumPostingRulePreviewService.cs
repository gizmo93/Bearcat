using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;

namespace Bearcat.Domain.UseCases.ManageForumPostingRules;

public class ForumPostingRulePreviewService(IForumPostingRuleReadRepository readRepository)
{
    public const int DefaultReleaseCount = 50;

    public async Task<IReadOnlyList<ForumPostingRulePreviewReadModel>> PreviewAsync(
        int distributionSiteRegistrationId,
        int releaseCount = DefaultReleaseCount,
        CancellationToken cancellationToken = default
    )
    {
        var rules = await readRepository.GetRulesForMatchingAsync(
            distributionSiteRegistrationId,
            cancellationToken
        );
        var releases = await readRepository.GetRecentReleasesForPreviewAsync(
            releaseCount,
            cancellationToken
        );

        var results = new List<ForumPostingRulePreviewReadModel>(releases.Count);

        foreach (var release in releases)
        {
            var context = ReleaseRoutingContext.FromRelease(release);
            var match = ForumPostingRuleMatcher.FindFirstMatch(rules, context);

            results.Add(
                new ForumPostingRulePreviewReadModel(
                    ReleaseId: release.Id,
                    ReleaseName: release.Name,
                    MatchedRuleName: match?.Name,
                    TargetPathSnapshot: match?.TargetPathSnapshot
                )
            );
        }

        return results;
    }
}
