using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPostingRules;

public class RuleFieldValuesTest
{
    [Test]
    public void FromRelease_WithClassification_ReadsCanonicalValues()
    {
        // Arrange
        var release = new Release
        {
            Name = "Some.Show.S02E05.1080p.WEB-DL-GROUP",
            ReleaseGroup = new ReleaseGroup { Name = "Series" },
            Classification = new ReleaseClassification
            {
                Title = "Some Show",
                Year = 2021,
                Season = 2,
                Episode = 5,
                ContentType = ReleaseContentType.TvShowEpisode,
                Platform = ReleasePlatform.Windows,
                Resolution = ReleaseResolution.R1080p,
                Source = ReleaseSource.WebDl,
                ReleaseGroupToken = "GROUP",
                PrimaryLanguage = "English",
                IsMultiLanguage = true,
            },
        };

        // Act
        var fieldValues = RuleFieldValues.FromRelease(release);

        // Assert
        fieldValues.ReleaseName.ShouldBe("Some.Show.S02E05.1080p.WEB-DL-GROUP");
        fieldValues.Resolution.ShouldBe(ReleaseResolution.R1080p);
        fieldValues.PrimaryLanguage.ShouldBe("English");
        fieldValues.IsMultiLanguage.ShouldBeTrue();
        fieldValues.ContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        fieldValues.Platform.ShouldBe(ReleasePlatform.Windows);
        fieldValues.Source.ShouldBe(ReleaseSource.WebDl);
        fieldValues.ReleaseGroupName.ShouldBe("Series");
        fieldValues.ReleaseGroupToken.ShouldBe("GROUP");
        fieldValues.Year.ShouldBe(2021);
        fieldValues.Season.ShouldBe(2);
        fieldValues.Episode.ShouldBe(5);
    }

    [Test]
    public void FromRelease_WithoutClassification_UsesSafeDefaults()
    {
        // Arrange
        var release = new Release { Name = "Some.Movie.2021-GROUP" };

        // Act
        var fieldValues = RuleFieldValues.FromRelease(release);

        // Assert
        fieldValues.Resolution.ShouldBe(ReleaseResolution.Unknown);
        fieldValues.Source.ShouldBe(ReleaseSource.Unknown);
        fieldValues.ContentType.ShouldBe(ReleaseContentType.Other);
        fieldValues.Platform.ShouldBe(ReleasePlatform.Unknown);
        fieldValues.PrimaryLanguage.ShouldBeNull();
        fieldValues.IsMultiLanguage.ShouldBeFalse();
        fieldValues.ReleaseGroupName.ShouldBeNull();
        fieldValues.ReleaseGroupToken.ShouldBeNull();
        fieldValues.Year.ShouldBeNull();
        fieldValues.Season.ShouldBeNull();
        fieldValues.Episode.ShouldBeNull();
    }

    [Test]
    public void FromRelease_UserPrimaryLanguageCode_WinsOverClassification()
    {
        // Arrange
        var release = new Release
        {
            Name = "Some.Movie.2021-GROUP",
            PrimaryLanguageCode = "fr",
            Classification = new ReleaseClassification
            {
                Title = "Some Movie",
                PrimaryLanguage = "German",
            },
        };

        // Act
        var fieldValues = RuleFieldValues.FromRelease(release);

        // Assert
        fieldValues.PrimaryLanguage.ShouldBe("French");
    }

    [Test]
    public void FromRelease_ReleaseContentTypeSetOnRelease_WinsOverClassification()
    {
        // Arrange
        var release = new Release
        {
            Name = "Some.Movie.2021-GROUP",
            ReleaseContentType = ReleaseContentType.Movie,
            Classification = new ReleaseClassification
            {
                Title = "Some Movie",
                ContentType = ReleaseContentType.TvShowEpisode,
            },
        };

        // Act
        var fieldValues = RuleFieldValues.FromRelease(release);

        // Assert
        fieldValues.ContentType.ShouldBe(ReleaseContentType.Movie);
    }

    [Test]
    public void FromReleaseCollection_ClassifiedMembersAgree_UsesUniformValues()
    {
        // Arrange
        var collection = CollectionWith(
            MemberWith(Classified(episode: 1)),
            MemberWith(Classified(episode: 2))
        );

        // Act
        var fieldValues = RuleFieldValues.FromReleaseCollection(collection);

        // Assert
        fieldValues.ReleaseName.ShouldBe("Some.Show.S02.1080p.WEB-DL-GROUP");
        fieldValues.ReleaseGroupName.ShouldBe("Series");
        fieldValues.Resolution.ShouldBe(ReleaseResolution.R1080p);
        fieldValues.Source.ShouldBe(ReleaseSource.WebDl);
        fieldValues.Platform.ShouldBe(ReleasePlatform.Windows);
        fieldValues.ContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        fieldValues.PrimaryLanguage.ShouldBe("English");
        fieldValues.IsMultiLanguage.ShouldBeTrue();
        fieldValues.ReleaseGroupToken.ShouldBe("GROUP");
        fieldValues.Year.ShouldBe(2021);
        fieldValues.Season.ShouldBe(2);
        fieldValues.Episode.ShouldBeNull();
    }

    [Test]
    public void FromReleaseCollection_ClassifiedMembersDisagree_UsesUnknownDefaults()
    {
        // Arrange
        var collection = CollectionWith(
            MemberWith(Classified(episode: 1)),
            MemberWith(
                new ReleaseClassification
                {
                    Title = "Some Show",
                    Year = 2022,
                    Season = 3,
                    ContentType = ReleaseContentType.Movie,
                    Platform = ReleasePlatform.Linux,
                    Resolution = ReleaseResolution.R720p,
                    Source = ReleaseSource.BluRay,
                    ReleaseGroupToken = "OTHER",
                    PrimaryLanguage = "German",
                    IsMultiLanguage = false,
                }
            )
        );

        // Act
        var fieldValues = RuleFieldValues.FromReleaseCollection(collection);

        // Assert
        fieldValues.Resolution.ShouldBe(ReleaseResolution.Unknown);
        fieldValues.Source.ShouldBe(ReleaseSource.Unknown);
        fieldValues.Platform.ShouldBe(ReleasePlatform.Unknown);
        fieldValues.ContentType.ShouldBe(ReleaseContentType.Other);
        fieldValues.PrimaryLanguage.ShouldBeNull();
        fieldValues.IsMultiLanguage.ShouldBeFalse();
        fieldValues.ReleaseGroupToken.ShouldBeNull();
        fieldValues.Year.ShouldBeNull();
        fieldValues.Season.ShouldBeNull();
    }

    [Test]
    public void FromReleaseCollection_UnclassifiedMembers_AreIgnored()
    {
        // Arrange
        var collection = CollectionWith(MemberWith(Classified(episode: 1)), MemberWith(null));

        // Act
        var fieldValues = RuleFieldValues.FromReleaseCollection(collection);

        // Assert
        fieldValues.Resolution.ShouldBe(ReleaseResolution.R1080p);
        fieldValues.ContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        fieldValues.PrimaryLanguage.ShouldBe("English");
        fieldValues.Season.ShouldBe(2);
    }

    [Test]
    public void FromReleaseCollection_NoClassifiedMembers_UsesSafeDefaults()
    {
        // Arrange
        var collection = CollectionWith(MemberWith(null), MemberWith(null));

        // Act
        var fieldValues = RuleFieldValues.FromReleaseCollection(collection);

        // Assert
        fieldValues.Resolution.ShouldBe(ReleaseResolution.Unknown);
        fieldValues.ContentType.ShouldBe(ReleaseContentType.Other);
        fieldValues.PrimaryLanguage.ShouldBeNull();
        fieldValues.IsMultiLanguage.ShouldBeFalse();
        fieldValues.Year.ShouldBeNull();
        fieldValues.Season.ShouldBeNull();
    }

    [Test]
    public void FromReleaseCollection_CollectionValuesSet_WinOverMembers()
    {
        // Arrange
        var collection = CollectionWith(
            MemberWith(Classified(episode: 1)),
            MemberWith(Classified(episode: 2))
        );
        collection.PrimaryLanguageCode = "fr";
        collection.ReleaseContentType = ReleaseContentType.Movie;

        // Act
        var fieldValues = RuleFieldValues.FromReleaseCollection(collection);

        // Assert
        fieldValues.PrimaryLanguage.ShouldBe("French");
        fieldValues.ContentType.ShouldBe(ReleaseContentType.Movie);
    }

    private static ReleaseCollection CollectionWith(params Release[] releases)
    {
        return new ReleaseCollection
        {
            Name = "Some.Show.S02.1080p.WEB-DL-GROUP",
            ReleaseGroup = new ReleaseGroup { Name = "Series" },
            Releases = releases.ToList(),
        };
    }

    private static Release MemberWith(ReleaseClassification? classification)
    {
        return new Release
        {
            Name = "Some.Show.S02E01.1080p.WEB-DL-GROUP",
            Classification = classification,
        };
    }

    private static ReleaseClassification Classified(int episode)
    {
        return new ReleaseClassification
        {
            Title = "Some Show",
            Year = 2021,
            Season = 2,
            Episode = episode,
            ContentType = ReleaseContentType.TvShowEpisode,
            Platform = ReleasePlatform.Windows,
            Resolution = ReleaseResolution.R1080p,
            Source = ReleaseSource.WebDl,
            ReleaseGroupToken = "GROUP",
            PrimaryLanguage = "English",
            IsMultiLanguage = true,
        };
    }
}
