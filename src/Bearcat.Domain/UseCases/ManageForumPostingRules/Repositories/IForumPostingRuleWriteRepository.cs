using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;

public interface IForumPostingRuleWriteRepository
{
    void Add(ForumPostingRule rule);

    void Remove(ForumPostingRule rule);

    Task<ForumPostingRule> GetByIdAsync(
        int forumPostingRuleId,
        CancellationToken cancellationToken = default
    );

    Task<List<ForumPostingRule>> GetByDistributionSiteRegistrationAsync(
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    );

    Task<int> GetNextSortOrderAsync(
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    );

    Task<ForumPostTemplateType?> GetForumPostTemplateTypeAsync(
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
