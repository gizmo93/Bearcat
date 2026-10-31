using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPostingRules;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.ForumPostingRules;

public class ReleaseRoutingContextTest
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
        var context = ReleaseRoutingContext.FromRelease(release);

        // Assert
        context.ReleaseName.ShouldBe("Some.Show.S02E05.1080p.WEB-DL-GROUP");
        context.Resolution.ShouldBe(ReleaseResolution.R1080p);
        context.PrimaryLanguage.ShouldBe("English");
        context.IsMultiLanguage.ShouldBeTrue();
        context.ContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        context.Platform.ShouldBe(ReleasePlatform.Windows);
        context.Source.ShouldBe(ReleaseSource.WebDl);
        context.ReleaseGroupName.ShouldBe("Series");
        context.ReleaseGroupToken.ShouldBe("GROUP");
        context.Year.ShouldBe(2021);
        context.Season.ShouldBe(2);
        context.Episode.ShouldBe(5);
        context.HasClassification.ShouldBeTrue();
    }

    [Test]
    public void FromRelease_WithoutClassification_UsesSafeDefaults()
    {
        // Arrange
        var release = new Release { Name = "Some.Movie.2021-GROUP" };

        // Act
        var context = ReleaseRoutingContext.FromRelease(release);

        // Assert
        context.Resolution.ShouldBe(ReleaseResolution.Unknown);
        context.Source.ShouldBe(ReleaseSource.Unknown);
        context.ContentType.ShouldBe(ReleaseContentType.Other);
        context.Platform.ShouldBe(ReleasePlatform.Unknown);
        context.PrimaryLanguage.ShouldBeNull();
        context.IsMultiLanguage.ShouldBeFalse();
        context.ReleaseGroupName.ShouldBeNull();
        context.ReleaseGroupToken.ShouldBeNull();
        context.Year.ShouldBeNull();
        context.Season.ShouldBeNull();
        context.Episode.ShouldBeNull();
        context.HasClassification.ShouldBeFalse();
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
        var context = ReleaseRoutingContext.FromRelease(release);

        // Assert
        context.PrimaryLanguage.ShouldBe("French");
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
        var context = ReleaseRoutingContext.FromRelease(release);

        // Assert
        context.ContentType.ShouldBe(ReleaseContentType.Movie);
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
        var context = ReleaseRoutingContext.FromReleaseCollection(collection);

        // Assert
        context.ReleaseName.ShouldBe("Some.Show.S02.1080p.WEB-DL-GROUP");
        context.ReleaseGroupName.ShouldBe("Series");
        context.Resolution.ShouldBe(ReleaseResolution.R1080p);
        context.Source.ShouldBe(ReleaseSource.WebDl);
        context.Platform.ShouldBe(ReleasePlatform.Windows);
        context.ContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        context.PrimaryLanguage.ShouldBe("English");
        context.IsMultiLanguage.ShouldBeTrue();
        context.ReleaseGroupToken.ShouldBe("GROUP");
        context.Year.ShouldBe(2021);
        context.Season.ShouldBe(2);
        context.Episode.ShouldBeNull();
        context.HasClassification.ShouldBeTrue();
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
        var context = ReleaseRoutingContext.FromReleaseCollection(collection);

        // Assert
        context.Resolution.ShouldBe(ReleaseResolution.Unknown);
        context.Source.ShouldBe(ReleaseSource.Unknown);
        context.Platform.ShouldBe(ReleasePlatform.Unknown);
        context.ContentType.ShouldBe(ReleaseContentType.Other);
        context.PrimaryLanguage.ShouldBeNull();
        context.IsMultiLanguage.ShouldBeFalse();
        context.ReleaseGroupToken.ShouldBeNull();
        context.Year.ShouldBeNull();
        context.Season.ShouldBeNull();
        context.HasClassification.ShouldBeTrue();
    }

    [Test]
    public void FromReleaseCollection_UnclassifiedMembers_AreIgnored()
    {
        // Arrange
        var collection = CollectionWith(MemberWith(Classified(episode: 1)), MemberWith(null));

        // Act
        var context = ReleaseRoutingContext.FromReleaseCollection(collection);

        // Assert
        context.Resolution.ShouldBe(ReleaseResolution.R1080p);
        context.ContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        context.PrimaryLanguage.ShouldBe("English");
        context.Season.ShouldBe(2);
        context.HasClassification.ShouldBeTrue();
    }

    [Test]
    public void FromReleaseCollection_NoClassifiedMembers_UsesSafeDefaults()
    {
        // Arrange
        var collection = CollectionWith(MemberWith(null), MemberWith(null));

        // Act
        var context = ReleaseRoutingContext.FromReleaseCollection(collection);

        // Assert
        context.Resolution.ShouldBe(ReleaseResolution.Unknown);
        context.ContentType.ShouldBe(ReleaseContentType.Other);
        context.PrimaryLanguage.ShouldBeNull();
        context.IsMultiLanguage.ShouldBeFalse();
        context.Year.ShouldBeNull();
        context.Season.ShouldBeNull();
        context.HasClassification.ShouldBeFalse();
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
        var context = ReleaseRoutingContext.FromReleaseCollection(collection);

        // Assert
        context.PrimaryLanguage.ShouldBe("French");
        context.ContentType.ShouldBe(ReleaseContentType.Movie);
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
