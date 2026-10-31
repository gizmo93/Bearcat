using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public static class AutoPostTestFactory
{
    public const string ReleaseName = "Some.Movie.2021.1080p.BluRay-GROUP";

    public const string ReleaseCollectionName = "Some.Show.S01.1080p.WEB-GROUP";

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
        int entityId = 1,
        int? distributionSiteRegistrationId = 5,
        int? forumPostTemplateId = null,
        string url = "https://forum.test/threads/1",
        DateTime? createdAt = null,
        DateTime? contentUpdatedAt = null
    )
    {
        return new AutoPostPostedLocation(
            PostedLocationId: postedLocationId,
            EntityId: entityId,
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

    public static ReleaseCollection ReleaseCollection(int id = 4, params Release[] releases)
    {
        return new ReleaseCollection
        {
            Id = id,
            Name = ReleaseCollectionName,
            ReleaseGroup = new ReleaseGroup { Name = "GROUP" },
            Releases = releases.Length == 0 ? [Release(id: 1), Release(id: 2)] : [.. releases],
        };
    }

    public static ForumPostingRule Rule(
        int id = 1,
        int sortOrder = 0,
        string[]? resolutions = null,
        ForumPostPostMode postMode = ForumPostPostMode.AlwaysNewThread,
        string? threadPrefixId = null,
        int forumPostTemplateId = 7,
        bool isEnabled = true,
        ForumPostTemplateType templateType = ForumPostTemplateType.Release
    )
    {
        return new ForumPostingRule
        {
            Id = id,
            SortOrder = sortOrder,
            Name = $"Rule {id}",
            ConditionJson = RuleConditionSerializer.Serialize(
                RuleCondition.CompareMany(
                    field: RuleFieldCatalog.Resolution,
                    conditionOperator: RuleConditionOperator.In,
                    values: resolutions ?? ["R1080p"]
                )
            ),
            TargetNodeId = "42",
            TargetPathSnapshot = "Board › Movies › HD",
            ThreadPrefixId = threadPrefixId,
            ForumPostTemplateId = forumPostTemplateId,
            ForumPostTemplate = new ForumPostTemplate
            {
                Id = forumPostTemplateId,
                Type = templateType,
            },
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
            DistributionSiteRegistrationId: id,
            Name: name,
            EnableAutomaticPosting: false,
            StripDotsForThreadSearch: true,
            EnabledRules: rules.Length == 0 ? [Rule()] : rules
        );
    }
}
