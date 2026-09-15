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
}
