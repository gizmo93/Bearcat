using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public static class AutoPostTestFactory
{
    public const string ReleaseName = "Some.Movie.2021.1080p.BluRay-GROUP";

    public static Release Release(
        int id = 1,
        QualityGateState qualityGateState = QualityGateState.Passed,
        ReleaseResolution resolution = ReleaseResolution.R1080p,
        bool withClassification = true
    )
    {
        return new Release
        {
            Id = id,
            Name = ReleaseName,
            QualityGateState = qualityGateState,
            Classification = withClassification
                ? new ReleaseClassification { Resolution = resolution }
                : null,
        };
    }

    public static ForumPostingRule Rule(
        int id = 1,
        int sortOrder = 0,
        string[]? resolutions = null,
        ForumPostPostMode postMode = ForumPostPostMode.AlwaysNewThread,
        string? threadPrefixId = null,
        int forumPostTemplateId = 7,
        bool isEnabled = true
    )
    {
        return new ForumPostingRule
        {
            Id = id,
            SortOrder = sortOrder,
            Name = $"Rule {id}",
            ConditionJson = RuleConditionSerializer.Serialize(
                RuleCondition.CompareMany(
                    RuleFieldCatalog.Resolution,
                    RuleConditionOperator.In,
                    resolutions ?? ["R1080p"]
                )
            ),
            TargetNodeId = "42",
            TargetPathSnapshot = "Board › Movies › HD",
            ThreadPrefixId = threadPrefixId,
            ForumPostTemplateId = forumPostTemplateId,
            PostMode = postMode,
            IsEnabled = isEnabled,
        };
    }

    public static AutoPostRegistration Registration(
        int id = 5,
        string name = "Board",
        params ForumPostingRule[] rules
    )
    {
        return new AutoPostRegistration(
            id,
            name,
            EnableAutomaticPosting: false,
            StripDotsForThreadSearch: true,
            rules.Length == 0 ? [Rule()] : rules
        );
    }
}
