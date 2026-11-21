using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPostingRules;

public class RuleConditionEvaluatorTest
{
    [Test]
    public void IsMatch_NotGermanAndHighDefinition_MatchesEnglishRelease()
    {
        // Arrange
        var condition = NotGermanAndHighDefinition();
        var fieldValues = CreateFieldValuesWith(
            primaryLanguage: "English",
            resolution: ReleaseResolution.R1080p
        );

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBeTrue();
    }

    [Test]
    public void IsMatch_NotGermanAndHighDefinition_DoesNotMatchGermanRelease()
    {
        // Arrange
        var condition = NotGermanAndHighDefinition();
        var fieldValues = CreateFieldValuesWith(
            primaryLanguage: "German",
            resolution: ReleaseResolution.R1080p
        );

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBeFalse();
    }

    [Test]
    public void IsMatch_NotGermanAndHighDefinition_DoesNotMatchStandardDefinition()
    {
        // Arrange
        var condition = NotGermanAndHighDefinition();
        var fieldValues = CreateFieldValuesWith(
            primaryLanguage: "English",
            resolution: ReleaseResolution.R480p
        );

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBeFalse();
    }

    [TestCase("%remux%", true)]
    [TestCase("%REMUX%", true)]
    [TestCase("some.movie%", true)]
    [TestCase("%-group", true)]
    [TestCase("%webrip%", false)]
    [TestCase("some.movie", false)]
    public void IsMatch_Like_UsesPercentWildcardCaseInsensitively(string pattern, bool expected)
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "ReleaseName",
            RuleConditionOperator.Like,
            pattern
        );
        var fieldValues = CreateFieldValuesWith(
            releaseName: "Some.Movie.2021.1080p.BluRay.REMUX-GROUP"
        );

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBe(expected);
    }

    [TestCase("Abenteuer, Animation, Familie", true)]
    [TestCase("Drama, Thriller", false)]
    [TestCase(null, false)]
    public void IsMatch_GenreLike_MatchesOneGenreInJoinedGenreList(string? genre, bool expected)
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "Genre",
            RuleConditionOperator.Like,
            "%animation%"
        );
        var fieldValues = CreateFieldValuesWith(genre: genre);

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBe(expected);
    }

    [Test]
    public void IsMatch_NotLike_NegatesLike()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "ReleaseName",
            RuleConditionOperator.NotLike,
            "%webrip%"
        );
        var fieldValues = CreateFieldValuesWith(
            releaseName: "Some.Movie.2021.1080p.BluRay.REMUX-GROUP"
        );

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBeTrue();
    }

    [TestCase(@"\d{4}", true)]
    [TestCase("bluray", true)]
    [TestCase("^Some", true)]
    [TestCase("^Other", false)]
    public void IsMatch_Regex_MatchesCaseInsensitively(string pattern, bool expected)
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "ReleaseName",
            RuleConditionOperator.Regex,
            pattern
        );
        var fieldValues = CreateFieldValuesWith(
            releaseName: "Some.Movie.2021.1080p.BluRay.REMUX-GROUP"
        );

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBe(expected);
    }

    [Test]
    public void IsMatch_InvalidRegexPattern_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "ReleaseName",
            RuleConditionOperator.Regex,
            "([unclosed"
        );
        var fieldValues = CreateFieldValuesWith(
            releaseName: "Some.Movie.2021.1080p.BluRay.REMUX-GROUP"
        );

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBeFalse();
    }

    [TestCase(ReleaseResolution.R2160p, true)]
    [TestCase(ReleaseResolution.R1080p, true)]
    [TestCase(ReleaseResolution.R720p, false)]
    [TestCase(ReleaseResolution.Unknown, false)]
    public void IsMatch_GreaterOrEqual_ComparesResolutionByEnumOrder(
        ReleaseResolution resolution,
        bool expected
    )
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "Resolution",
            RuleConditionOperator.GreaterOrEqual,
            "R1080p"
        );
        var fieldValues = CreateFieldValuesWith(resolution: resolution);

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBe(expected);
    }

    [Test]
    public void IsMatch_IntegerRange_MatchesYearBetweenBounds()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateAll(
            RuleConditionFactory.CreateComparison(
                "Year",
                RuleConditionOperator.GreaterOrEqual,
                "2000"
            ),
            RuleConditionFactory.CreateComparison("Year", RuleConditionOperator.LessOrEqual, "2010")
        );

        // Act + Assert
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(year: 2005))
            .ShouldBeTrue();
        RuleConditionEvaluator.IsMatch(condition, CreateFieldValuesWith(year: 2000)).ShouldBeTrue();
        RuleConditionEvaluator.IsMatch(condition, CreateFieldValuesWith(year: 2010)).ShouldBeTrue();
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(year: 1999))
            .ShouldBeFalse();
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(year: null))
            .ShouldBeFalse();
    }

    [Test]
    public void IsMatch_IsSetAndIsNotSet_HandleNullableFields()
    {
        // Arrange
        var isSet = RuleConditionFactory.CreateComparison(
            "Season",
            RuleConditionOperator.IsSet,
            string.Empty
        );
        var isNotSet = RuleConditionFactory.CreateComparison(
            "PrimaryLanguage",
            RuleConditionOperator.IsNotSet,
            string.Empty
        );

        // Act + Assert
        RuleConditionEvaluator.IsMatch(isSet, CreateFieldValuesWith(season: 3)).ShouldBeTrue();
        RuleConditionEvaluator.IsMatch(isSet, CreateFieldValuesWith(season: null)).ShouldBeFalse();
        RuleConditionEvaluator
            .IsMatch(isNotSet, CreateFieldValuesWith(primaryLanguage: null))
            .ShouldBeTrue();
        RuleConditionEvaluator
            .IsMatch(isNotSet, CreateFieldValuesWith(primaryLanguage: "German"))
            .ShouldBeFalse();
    }

    [TestCase(true, "true", true)]
    [TestCase(true, "false", false)]
    [TestCase(false, "false", true)]
    [TestCase(false, "True", false)]
    public void IsMatch_Boolean_ComparesIsMultiLanguage(
        bool isMultiLanguage,
        string value,
        bool expected
    )
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "IsMultiLanguage",
            RuleConditionOperator.Equals,
            value
        );
        var fieldValues = CreateFieldValuesWith(isMultiLanguage: isMultiLanguage);

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBe(expected);
    }

    [Test]
    public void IsMatch_TextEquals_IgnoresCase()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "PrimaryLanguage",
            RuleConditionOperator.Equals,
            "german"
        );
        var fieldValues = CreateFieldValuesWith(primaryLanguage: "German");

        // Act
        var isMatch = RuleConditionEvaluator.IsMatch(condition, fieldValues);

        // Assert
        isMatch.ShouldBeTrue();
    }

    [Test]
    public void IsMatch_NotIn_IsTrueWhenValueIsMissing()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateMultiValueComparison(
            "PrimaryLanguage",
            RuleConditionOperator.NotIn,
            "German",
            "French"
        );

        // Act + Assert
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(primaryLanguage: "English"))
            .ShouldBeTrue();
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(primaryLanguage: "german"))
            .ShouldBeFalse();
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(primaryLanguage: null))
            .ShouldBeTrue();
    }

    [Test]
    public void IsMatch_PlatformEquals_MatchesWindowsOnly()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateComparison(
            "Platform",
            RuleConditionOperator.Equals,
            "Windows"
        );

        // Act + Assert
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(platform: ReleasePlatform.Windows))
            .ShouldBeTrue();
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(platform: ReleasePlatform.Unknown))
            .ShouldBeFalse();
    }

    [Test]
    public void IsMatch_PlatformIn_MatchesAnyListedPlatform()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateMultiValueComparison(
            "Platform",
            RuleConditionOperator.In,
            "Windows",
            "NintendoSwitch"
        );

        // Act + Assert
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(platform: ReleasePlatform.Windows))
            .ShouldBeTrue();
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(platform: ReleasePlatform.NintendoSwitch))
            .ShouldBeTrue();
        RuleConditionEvaluator
            .IsMatch(condition, CreateFieldValuesWith(platform: ReleasePlatform.Unknown))
            .ShouldBeFalse();
    }

    [Test]
    public void IsMatch_DeeplyNestedTree_CombinesAllAnyAndNot()
    {
        // Arrange
        var condition = RuleConditionFactory.CreateAll(
            RuleConditionFactory.CreateComparison(
                "ContentType",
                RuleConditionOperator.Equals,
                "TvShowEpisode"
            ),
            RuleConditionFactory.CreateAny(
                RuleConditionFactory.CreateAll(
                    RuleConditionFactory.CreateComparison(
                        "Season",
                        RuleConditionOperator.Equals,
                        "2"
                    ),
                    RuleConditionFactory.CreateNot(
                        RuleConditionFactory.CreateMultiValueComparison(
                            "Source",
                            RuleConditionOperator.In,
                            "Hdtv",
                            "TvRip"
                        )
                    )
                ),
                RuleConditionFactory.CreateComparison(
                    "ReleaseGroupToken",
                    RuleConditionOperator.Equals,
                    "GROUP"
                )
            )
        );

        var matching = CreateFieldValuesWith(
            contentType: ReleaseContentType.TvShowEpisode,
            season: 2,
            source: ReleaseSource.WebDl,
            releaseGroupToken: "OTHER"
        );
        var nonMatching = CreateFieldValuesWith(
            contentType: ReleaseContentType.TvShowEpisode,
            season: 2,
            source: ReleaseSource.Hdtv,
            releaseGroupToken: "OTHER"
        );
        var matchingByToken = CreateFieldValuesWith(
            contentType: ReleaseContentType.TvShowEpisode,
            season: 9,
            source: ReleaseSource.Hdtv,
            releaseGroupToken: "group"
        );

        // Act + Assert
        RuleConditionEvaluator.IsMatch(condition, matching).ShouldBeTrue();
        RuleConditionEvaluator.IsMatch(condition, nonMatching).ShouldBeFalse();
        RuleConditionEvaluator.IsMatch(condition, matchingByToken).ShouldBeTrue();
    }

    private static RuleCondition NotGermanAndHighDefinition()
    {
        return RuleConditionFactory.CreateAll(
            RuleConditionFactory.CreateNot(
                RuleConditionFactory.CreateComparison(
                    "PrimaryLanguage",
                    RuleConditionOperator.Equals,
                    "German"
                )
            ),
            RuleConditionFactory.CreateMultiValueComparison(
                "Resolution",
                RuleConditionOperator.In,
                "R1080p",
                "R720p"
            )
        );
    }

    private static RuleFieldValues CreateFieldValuesWith(
        string releaseName = "Some.Movie.2021.1080p.BluRay.REMUX-GROUP",
        ReleaseResolution resolution = ReleaseResolution.R1080p,
        string? primaryLanguage = "English",
        bool isMultiLanguage = false,
        ReleaseContentType contentType = ReleaseContentType.Movie,
        ReleasePlatform platform = ReleasePlatform.Unknown,
        ReleaseSource source = ReleaseSource.BluRay,
        string? releaseGroupName = "Movies",
        string? releaseGroupToken = "GROUP",
        int? year = 2021,
        int? season = null,
        int? episode = null,
        string? genre = null
    )
    {
        return new RuleFieldValues(
            ReleaseName: releaseName,
            Resolution: resolution,
            PrimaryLanguage: primaryLanguage,
            IsMultiLanguage: isMultiLanguage,
            ContentType: contentType,
            Platform: platform,
            Source: source,
            ReleaseGroupName: releaseGroupName,
            ReleaseGroupToken: releaseGroupToken,
            Year: year,
            Season: season,
            Episode: episode,
            Genre: genre
        );
    }
}
