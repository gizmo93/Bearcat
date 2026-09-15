using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseNameParsing;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageReleases.ReleaseNameParsing;

public class ReleaseClassificationBuilderTest
{
    [Test]
    public void Build_ResolutionInName_TakesResolutionFromName()
    {
        // Arrange
        var mediaFiles = new List<ReleaseMediaFile> { VideoFile(height: 576) };

        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "The.Matrix.1999.1080p.BluRay.x264-GROUP",
            mediaFiles
        );

        // Assert
        classification.Resolution.ShouldBe(ReleaseResolution.R1080p);
        classification.ResolutionSource.ShouldBe(ClassificationSource.ReleaseName);
    }

    [Test]
    public void Build_NoResolutionInName_FallsBackToMediaInfoHeight()
    {
        // Arrange
        var mediaFiles = new List<ReleaseMediaFile> { VideoFile(height: 576, ("de", true)) };

        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Black.Diamond.2025.German.BDRip.x264-CPTN",
            mediaFiles
        );

        // Assert
        classification.Resolution.ShouldBe(ReleaseResolution.R576p);
        classification.ResolutionSource.ShouldBe(ClassificationSource.MediaInfo);
    }

    [TestCase(2160, ReleaseResolution.R2160p)]
    [TestCase(1080, ReleaseResolution.R1080p)]
    [TestCase(720, ReleaseResolution.R720p)]
    [TestCase(576, ReleaseResolution.R576p)]
    [TestCase(480, ReleaseResolution.R480p)]
    public void Build_MediaInfoHeight_MapsToResolution(int height, ReleaseResolution expected)
    {
        // Arrange
        var mediaFiles = new List<ReleaseMediaFile> { VideoFile(height) };

        // Act
        var classification = ReleaseClassificationBuilder.Build("Plain.Folder", mediaFiles);

        // Assert
        classification.Resolution.ShouldBe(expected);
    }

    [Test]
    public void Build_LanguageInName_TakesLanguageFromName()
    {
        // Arrange
        var mediaFiles = new List<ReleaseMediaFile> { VideoFile(1080, ("en", true)) };

        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Amok.1994.German.DL.1080p.BluRay.x264-PL3X",
            mediaFiles
        );

        // Assert
        classification.PrimaryLanguage.ShouldBe("German");
        classification.LanguageSource.ShouldBe(ClassificationSource.ReleaseName);
        classification.IsMultiLanguage.ShouldBeTrue();
    }

    [Test]
    public void Build_NoLanguageInName_FallsBackToDefaultAudioStream()
    {
        // Arrange
        var mediaFiles = new List<ReleaseMediaFile>
        {
            VideoFile(1080, ("en", false), ("de", true)),
        };

        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Some.Movie.2020-GROUP",
            mediaFiles
        );

        // Assert
        classification.PrimaryLanguage.ShouldBe("German");
        classification.LanguageSource.ShouldBe(ClassificationSource.MediaInfo);
        classification.IsMultiLanguage.ShouldBeTrue();
    }

    [Test]
    public void Build_SingleAudioLanguage_IsNotMultiLanguage()
    {
        // Arrange
        var mediaFiles = new List<ReleaseMediaFile> { VideoFile(1080, ("de", true)) };

        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Some.Movie.2020-GROUP",
            mediaFiles
        );

        // Assert
        classification.PrimaryLanguage.ShouldBe("German");
        classification.IsMultiLanguage.ShouldBeFalse();
    }

    [Test]
    public void Build_CarriesTitleYearSeasonEpisodeFromName()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Breaking.Bad.S03E10.720p.HDTV.x264-LOL",
            []
        );

        // Assert
        classification.Title.ShouldBe("Breaking Bad");
        classification.Season.ShouldBe(3);
        classification.Episode.ShouldBe(10);
        classification.ParserVersion.ShouldBe(ReleaseClassificationBuilder.CurrentParserVersion);
    }

    [Test]
    public void Build_NoMediaAndNoResolutionInName_LeavesResolutionUnknown()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Some.Old.Movie.1985.German.DVDRip.x264-GRP",
            []
        );

        // Assert
        classification.Resolution.ShouldBe(ReleaseResolution.Unknown);
        classification.ResolutionSource.ShouldBe(ClassificationSource.None);
        classification.PrimaryLanguage.ShouldBe("German");
        classification.LanguageSource.ShouldBe(ClassificationSource.ReleaseName);
    }

    [Test]
    public void Build_GermanMovie_CarriesSourceGroupTokenAndContentType()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Movie.Name.2023.German.DL.1080p.BluRay.x264-GROUP",
            []
        );

        // Assert
        classification.Source.ShouldBe(ReleaseSource.BluRay);
        classification.SourceSource.ShouldBe(ClassificationSource.ReleaseName);
        classification.ReleaseGroupToken.ShouldBe("GROUP");
        classification.ContentType.ShouldBe(ReleaseContentType.Movie);
        classification.EpisodeEnd.ShouldBeNull();
    }

    [Test]
    public void Build_GermanEpisode_CarriesSourceGroupTokenAndContentType()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Show.Name.S01E03.German.720p.WEB.h264-GRP",
            []
        );

        // Assert
        classification.Source.ShouldBe(ReleaseSource.Web);
        classification.SourceSource.ShouldBe(ClassificationSource.ReleaseName);
        classification.ReleaseGroupToken.ShouldBe("GRP");
        classification.ContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        classification.Season.ShouldBe(1);
        classification.Episode.ShouldBe(3);
    }

    [Test]
    public void Build_MultiEpisode_CarriesEpisodeEnd()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Show.Name.S02E05-E06.German.DL.1080p.WEB-DL.x264-TVS",
            []
        );

        // Assert
        classification.Episode.ShouldBe(5);
        classification.EpisodeEnd.ShouldBe(6);
        classification.ContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        classification.Source.ShouldBe(ReleaseSource.WebDl);
    }

    [Test]
    public void Build_NoSourceInName_LeavesSourceUnknown()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build("Plain.Folder", []);

        // Assert
        classification.Source.ShouldBe(ReleaseSource.Unknown);
        classification.SourceSource.ShouldBe(ClassificationSource.None);
        classification.ReleaseGroupToken.ShouldBeNull();
        classification.ContentType.ShouldBe(ReleaseContentType.Other);
    }

    [TestCase("Movie.Name.2023.German.DL.1080p.BluRay.x264-GROUP", ReleaseContentType.Movie)]
    [TestCase("Show.Name.S01E03.German.720p.WEB.h264-GRP", ReleaseContentType.TvShowEpisode)]
    [TestCase("Breaking.Bad.S03.1080p.BluRay.x264-GROUP", ReleaseContentType.TvShowEpisode)]
    [TestCase("Some.Movie.German.1080p.BluRay.x264-GRP", ReleaseContentType.Movie)]
    [TestCase("My Home Videos 2019", ReleaseContentType.Movie)]
    [TestCase("My Home Videos", ReleaseContentType.Other)]
    public void Build_ContentType_IsDerivedFromName(string releaseName, ReleaseContentType expected)
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(releaseName, []);

        // Assert
        classification.ContentType.ShouldBe(expected);
    }

    private static ReleaseMediaFile VideoFile(
        int? height,
        params (string Language, bool IsDefault)[] audioStreams
    )
    {
        var tracks = new List<string> { "{\"@type\":\"General\"}" };

        if (height is not null)
        {
            tracks.Add($"{{\"@type\":\"Video\",\"Width\":\"1920\",\"Height\":\"{height}\"}}");
        }

        foreach (var (language, isDefault) in audioStreams)
        {
            tracks.Add(
                $"{{\"@type\":\"Audio\",\"Language\":\"{language}\",\"Default\":\"{(isDefault ? "Yes" : "No")}\"}}"
            );
        }

        var json = "{\"media\":{\"track\":[" + string.Join(",", tracks) + "]}}";

        return new ReleaseMediaFile
        {
            RelativePath = "movie.mkv",
            SizeBytes = 1,
            MediaInfoJson = json,
            MediaInfoText = string.Empty,
        };
    }
}
