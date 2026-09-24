using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Dto;
using Bearcat.Domain.UseCases.ManageForumPostingRules.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageForumPostingRules;

public class ForumPostingRuleService(IForumPostingRuleWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        int distributionSiteRegistrationId,
        ForumPostingRuleInput input,
        CancellationToken cancellationToken = default
    )
    {
        await ValidateAsync(input, cancellationToken);

        var now = DateTime.UtcNow;
        var rule = new ForumPostingRule
        {
            DistributionSiteRegistrationId = distributionSiteRegistrationId,
            SortOrder = await writeRepository.GetNextSortOrderAsync(
                distributionSiteRegistrationId,
                cancellationToken
            ),
            Name = input.Name.Trim(),
            ConditionJson = input.ConditionJson.Trim(),
            TargetNodeId = input.TargetNodeId.Trim(),
            TargetPathSnapshot = input.TargetPathSnapshot.Trim(),
            ThreadPrefixId = NormalizeOptional(input.ThreadPrefixId),
            ForumPostTemplateId = input.ForumPostTemplateId,
            PostMode = input.PostMode,
            IsEnabled = input.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now,
        };

        writeRepository.Add(rule);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return rule.Id;
    }

    public async Task UpdateAsync(
        int forumPostingRuleId,
        ForumPostingRuleInput input,
        CancellationToken cancellationToken = default
    )
    {
        await ValidateAsync(input, cancellationToken);

        var rule = await writeRepository.GetByIdAsync(forumPostingRuleId, cancellationToken);
        rule.Name = input.Name.Trim();
        rule.ConditionJson = input.ConditionJson.Trim();
        rule.TargetNodeId = input.TargetNodeId.Trim();
        rule.TargetPathSnapshot = input.TargetPathSnapshot.Trim();
        rule.ThreadPrefixId = NormalizeOptional(input.ThreadPrefixId);
        rule.ForumPostTemplateId = input.ForumPostTemplateId;
        rule.PostMode = input.PostMode;
        rule.IsEnabled = input.IsEnabled;
        rule.UpdatedAt = DateTime.UtcNow;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int forumPostingRuleId,
        CancellationToken cancellationToken = default
    )
    {
        var rule = await writeRepository.GetByIdAsync(forumPostingRuleId, cancellationToken);
        writeRepository.Remove(rule);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(
        int distributionSiteRegistrationId,
        IReadOnlyList<int> orderedForumPostingRuleIds,
        CancellationToken cancellationToken = default
    )
    {
        var rules = await writeRepository.GetByDistributionSiteRegistrationAsync(
            distributionSiteRegistrationId,
            cancellationToken
        );
        var now = DateTime.UtcNow;

        for (var index = 0; index < orderedForumPostingRuleIds.Count; index++)
        {
            var rule = rules.FirstOrDefault(candidate =>
                candidate.Id == orderedForumPostingRuleIds[index]
            );

            if (rule is null)
            {
                throw new ArgumentException(
                    $"The posting rule {orderedForumPostingRuleIds[index]} does not belong to the distribution site registration {distributionSiteRegistrationId}."
                );
            }

            rule.SortOrder = index;
            rule.UpdatedAt = now;
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleIsEnabledAsync(
        int forumPostingRuleId,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var rule = await writeRepository.GetByIdAsync(forumPostingRuleId, cancellationToken);
        rule.IsEnabled = isEnabled;
        rule.UpdatedAt = DateTime.UtcNow;

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateAsync(
        ForumPostingRuleInput input,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ArgumentException("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(input.TargetNodeId))
        {
            throw new ArgumentException("Target node id is required.");
        }

        var templateType = await writeRepository.GetForumPostTemplateTypeAsync(
            input.ForumPostTemplateId,
            cancellationToken
        );

        if (templateType is null)
        {
            throw new ArgumentException(
                $"The forum post template {input.ForumPostTemplateId} does not exist."
            );
        }

        if (templateType != ForumPostTemplateType.Release)
        {
            throw new ArgumentException("The forum post template must be a release template.");
        }

        var errors = RuleConditionSerializer.ValidateJson(
            input.ConditionJson,
            RuleFieldCatalog.Fields
        );

        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
