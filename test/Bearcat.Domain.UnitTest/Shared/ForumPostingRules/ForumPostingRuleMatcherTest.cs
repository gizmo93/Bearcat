using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPostingRules;

public class ForumPostingRuleMatcherTest
{
    [Test]
    public void FindFirstMatch_SeveralMatchingRules_ReturnsLowestSortOrder()
    {
        // Arrange
        var rules = new List<ForumPostingRule>
        {
            RuleWith(id: 1, sortOrder: 2, condition: ResolutionIn("R1080p")),
            RuleWith(id: 2, sortOrder: 0, condition: ResolutionIn("R1080p", "R720p")),
            RuleWith(id: 3, sortOrder: 1, condition: ResolutionIn("R1080p")),
        };

        // Act
        var match = ForumPostingRuleMatcher.FindFirstMatch(
            rules: rules,
            entityType: ForumPostTemplateType.Release,
            context: Context()
        );

        // Assert
        match.ShouldNotBeNull().Id.ShouldBe(2);
    }

    [Test]
    public void FindFirstMatch_DisabledRule_IsSkipped()
    {
        // Arrange
        var rules = new List<ForumPostingRule>
        {
            RuleWith(id: 1, sortOrder: 0, condition: ResolutionIn("R1080p"), isEnabled: false),
            RuleWith(id: 2, sortOrder: 1, condition: ResolutionIn("R1080p")),
        };

        // Act
        var match = ForumPostingRuleMatcher.FindFirstMatch(
            rules: rules,
            entityType: ForumPostTemplateType.Release,
            context: Context()
        );

        // Assert
        match.ShouldNotBeNull().Id.ShouldBe(2);
    }

    [Test]
    public void FindFirstMatch_BrokenConditionJson_IsSkipped()
    {
        // Arrange
        var rules = new List<ForumPostingRule>
        {
            RuleWith(id: 1, sortOrder: 0, conditionJson: "{ broken"),
            RuleWith(id: 2, sortOrder: 1, condition: ResolutionIn("R1080p")),
        };

        // Act
        var match = ForumPostingRuleMatcher.FindFirstMatch(
            rules: rules,
            entityType: ForumPostTemplateType.Release,
            context: Context()
        );

        // Assert
        match.ShouldNotBeNull().Id.ShouldBe(2);
    }

    [Test]
    public void FindFirstMatch_NothingMatches_ReturnsNull()
    {
        // Arrange
        var rules = new List<ForumPostingRule>
        {
            RuleWith(id: 1, sortOrder: 0, condition: ResolutionIn("R720p")),
            RuleWith(id: 2, sortOrder: 1, condition: ResolutionIn("Sd")),
        };

        // Act
        var match = ForumPostingRuleMatcher.FindFirstMatch(
            rules: rules,
            entityType: ForumPostTemplateType.Release,
            context: Context()
        );

        // Assert
        match.ShouldBeNull();
    }

    [Test]
    public void FindFirstMatch_NoRules_ReturnsNull()
    {
        // Act
        var match = ForumPostingRuleMatcher.FindFirstMatch(
            rules: [],
            entityType: ForumPostTemplateType.Release,
            context: Context()
        );

        // Assert
        match.ShouldBeNull();
    }

    [Test]
    public void FindFirstMatch_ReleaseEntityType_SkipsCollectionRules()
    {
        // Arrange
        var rules = new List<ForumPostingRule>
        {
            RuleWith(id: 1, sortOrder: 0, templateType: ForumPostTemplateType.ReleaseCollection),
            RuleWith(id: 2, sortOrder: 1, templateType: ForumPostTemplateType.Release),
        };

        // Act
        var match = ForumPostingRuleMatcher.FindFirstMatch(
            rules: rules,
            entityType: ForumPostTemplateType.Release,
            context: Context()
        );

        // Assert
        match.ShouldNotBeNull().Id.ShouldBe(2);
    }

    [Test]
    public void FindFirstMatch_ReleaseCollectionEntityType_SkipsReleaseRules()
    {
        // Arrange
        var rules = new List<ForumPostingRule>
        {
            RuleWith(id: 1, sortOrder: 0, templateType: ForumPostTemplateType.Release),
            RuleWith(id: 2, sortOrder: 1, templateType: ForumPostTemplateType.ReleaseCollection),
        };

        // Act
        var match = ForumPostingRuleMatcher.FindFirstMatch(
            rules: rules,
            entityType: ForumPostTemplateType.ReleaseCollection,
            context: Context()
        );

        // Assert
        match.ShouldNotBeNull().Id.ShouldBe(2);
    }

    private static RuleCondition ResolutionIn(params string[] resolutions)
    {
        return RuleCondition.CompareMany(
            RuleFieldCatalog.Resolution,
            RuleConditionOperator.In,
            resolutions
        );
    }

    private static ForumPostingRule RuleWith(
        int id,
        int sortOrder,
        RuleCondition? condition = null,
        string? conditionJson = null,
        bool isEnabled = true,
        ForumPostTemplateType templateType = ForumPostTemplateType.Release
    )
    {
        return new ForumPostingRule
        {
            Id = id,
            SortOrder = sortOrder,
            Name = $"Rule {id}",
            ConditionJson =
                conditionJson
                ?? RuleConditionSerializer.Serialize(condition ?? ResolutionIn("R1080p")),
            TargetNodeId = "42",
            TargetPathSnapshot = "Board > Movies > HD",
            ForumPostTemplateId = 1,
            ForumPostTemplate = new ForumPostTemplate { Id = 1, Type = templateType },
            PostMode = ForumPostPostMode.AlwaysNewThread,
            IsEnabled = isEnabled,
        };
    }

    private static ReleaseRoutingContext Context()
    {
        return new ReleaseRoutingContext(
            ReleaseName: "Some.Movie.2021.1080p.BluRay.REMUX-GROUP",
            Resolution: ReleaseResolution.R1080p,
            PrimaryLanguage: "English",
            IsMultiLanguage: false,
            ContentType: ReleaseContentType.Movie,
            Platform: ReleasePlatform.Unknown,
            Source: ReleaseSource.BluRay,
            ReleaseGroupName: "Movies",
            ReleaseGroupToken: "GROUP",
            Year: 2021,
            Season: null,
            Episode: null,
            HasClassification: true
        );
    }
}
