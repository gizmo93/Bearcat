using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;

namespace Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;

public interface IForumPostingRuleReadRepository
{
    Task<IReadOnlyList<ForumPostingRuleSummaryReadModel>> GetAllAsync(
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    );

    Task<ForumPostingRuleDetailReadModel?> GetDetailAsync(
        int forumPostingRuleId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ForumPostingRule>> GetRulesForMatchingAsync(
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Release>> GetRecentReleasesForPreviewAsync(
        int count,
        CancellationToken cancellationToken = default
    );
}
