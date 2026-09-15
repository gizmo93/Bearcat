using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageForumPostingRules.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ForumPostingRuleRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IForumPostingRuleReadRepository, IForumPostingRuleWriteRepository
{
    public async Task<IReadOnlyList<ForumPostingRuleSummaryReadModel>> GetAllAsync(
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ForumPostingRules.Where(rule =>
                rule.DistributionSiteRegistrationId == distributionSiteRegistrationId
            )
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .Select(rule => new ForumPostingRuleSummaryReadModel(
                rule.Id,
                rule.DistributionSiteRegistrationId,
                rule.SortOrder,
                rule.Name,
                rule.TargetNodeId,
                rule.TargetPathSnapshot,
                rule.ThreadPrefixId,
                rule.ForumPostTemplateId,
                rule.ForumPostTemplate.Name,
                rule.PostMode,
                rule.IsEnabled,
                rule.UpdatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ForumPostingRuleDetailReadModel?> GetDetailAsync(
        int forumPostingRuleId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ForumPostingRules.Where(rule => rule.Id == forumPostingRuleId)
            .Select(rule => new ForumPostingRuleDetailReadModel(
                rule.Id,
                rule.DistributionSiteRegistrationId,
                rule.SortOrder,
                rule.Name,
                rule.ConditionJson,
                rule.TargetNodeId,
                rule.TargetPathSnapshot,
                rule.ThreadPrefixId,
                rule.ForumPostTemplateId,
                rule.ForumPostTemplate.Name,
                rule.PostMode,
                rule.StripDotsForThreadSearch,
                rule.IsEnabled
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumPostingRule>> GetRulesForMatchingAsync(
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ForumPostingRules.Where(rule =>
                rule.DistributionSiteRegistrationId == distributionSiteRegistrationId
            )
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Release>> GetRecentReleasesForPreviewAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Releases.Include(release => release.Classification)
            .Include(release => release.ReleaseGroup)
            .OrderByDescending(release => release.CreatedAt)
            .ThenByDescending(release => release.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public void Add(ForumPostingRule rule)
    {
        dbWrite.Add(rule);
    }

    public void Remove(ForumPostingRule rule)
    {
        dbWrite.Remove(rule);
    }

    public async Task<ForumPostingRule> GetByIdAsync(
        int forumPostingRuleId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ForumPostingRules.FirstAsync(
            rule => rule.Id == forumPostingRuleId,
            cancellationToken
        );
    }

    public async Task<List<ForumPostingRule>> GetByDistributionSiteRegistrationAsync(
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ForumPostingRules.Where(rule =>
                rule.DistributionSiteRegistrationId == distributionSiteRegistrationId
            )
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetNextSortOrderAsync(
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var highestSortOrder = await dbRead
            .ForumPostingRules.Where(rule =>
                rule.DistributionSiteRegistrationId == distributionSiteRegistrationId
            )
            .Select(rule => (int?)rule.SortOrder)
            .MaxAsync(cancellationToken);

        return highestSortOrder is null ? 0 : highestSortOrder.Value + 1;
    }

    public async Task<ForumPostTemplateType?> GetForumPostTemplateTypeAsync(
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ForumPostTemplates.Where(template => template.Id == forumPostTemplateId)
            .Select(template => (ForumPostTemplateType?)template.Type)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
