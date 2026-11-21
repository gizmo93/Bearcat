using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class ForumPostingRuleMatcher
{
    public static ForumPostingRule? FindFirstMatch(
        IReadOnlyList<ForumPostingRule> rules,
        ForumPostTemplateType entityType,
        ReleaseRoutingContext context
    )
    {
        var candidates = rules
            .Where(rule => rule.IsEnabled && rule.ForumPostTemplate.Type == entityType)
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .ToList();

        return candidates.FirstOrDefault(rule =>
            RuleConditionEvaluator.IsMatch(
                RuleConditionSerializer.Deserialize(rule.ConditionJson),
                context
            )
        );
    }
}
