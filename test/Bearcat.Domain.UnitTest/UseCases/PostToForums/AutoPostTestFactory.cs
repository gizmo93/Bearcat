using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public static class AutoPostTestFactory
{
    public const string ReleaseName = "Some.Movie.2021.1080p.BluRay-GROUP";

    public static readonly DateTime PostedAt = new(2026, 9, 10, 20, 0, 0, DateTimeKind.Unspecified);

    public static readonly DateTime ReuploadedAt = new(
        2026,
        9,
        14,
        20,
        0,
        0,
        DateTimeKind.Unspecified
    );

    public static AutoPostPostedLocation PostedLocation(
        int postedLocationId = 1,
        int releaseId = 1,
        int? distributionSiteRegistrationId = 5,
        int? forumPostTemplateId = null,
        string url = "https://forum.test/threads/1",
        DateTime? createdAt = null,
        DateTime? contentUpdatedAt = null
    )
    {
        return new AutoPostPostedLocation(
            PostedLocationId: postedLocationId,
            ReleaseId: releaseId,
            DistributionSiteRegistrationId: distributionSiteRegistrationId,
            ForumPostTemplateId: forumPostTemplateId,
            Url: url,
            CreatedAt: createdAt ?? PostedAt,
            ContentUpdatedAt: contentUpdatedAt
        );
    }

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
