using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public static class ForumPostingRuleMatcher
{
    public static ForumPostingRule? FindFirstMatch(
        IReadOnlyList<ForumPostingRule> rules,
        ReleaseRoutingContext context
    )
    {
        var candidates = rules
            .Where(rule => rule.IsEnabled)
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Id)
            .ToList();

        foreach (var rule in candidates)
        {
            if (!RuleConditionSerializer.TryDeserialize(rule.ConditionJson, out var condition))
            {
                continue;
            }

            if (RuleConditionEvaluator.IsMatch(condition, context))
            {
                return rule;
            }
        }

        return null;
    }
}
