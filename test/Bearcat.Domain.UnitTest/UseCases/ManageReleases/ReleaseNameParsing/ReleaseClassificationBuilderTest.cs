using Bearcat.Abstractions.NfoDatabase;
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
            mediaFiles,
            contentKind: null,
            nfoContent: null
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
            mediaFiles,
            contentKind: null,
            nfoContent: null
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
        var classification = ReleaseClassificationBuilder.Build(
            "Plain.Folder",
            mediaFiles,
            contentKind: null,
            nfoContent: null
        );

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
            mediaFiles,
            contentKind: null,
            nfoContent: null
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
            mediaFiles,
            contentKind: null,
            nfoContent: null
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
            mediaFiles,
            contentKind: null,
            nfoContent: null
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
            [],
            contentKind: null,
            nfoContent: null
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
            [],
            contentKind: null,
            nfoContent: null
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
            [],
            contentKind: null,
            nfoContent: null
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
            [],
            contentKind: null,
            nfoContent: null
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
            [],
            contentKind: null,
            nfoContent: null
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
        var classification = ReleaseClassificationBuilder.Build(
            "Plain.Folder",
            [],
            contentKind: null,
            nfoContent: null
        );

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
        var classification = ReleaseClassificationBuilder.Build(
            releaseName,
            [],
            contentKind: null,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldBe(expected);
    }

    [TestCase("Sunken.Realms-RUNE")]
    [TestCase("Nightwater-TENOKE")]
    public void Build_GameReleaseGroup_IsClassifiedAsGame(string releaseName)
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            releaseName,
            [],
            contentKind: null,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Game);
        classification.ContentTypeSource.ShouldBe(ClassificationSource.ReleaseName);
    }

    [TestCase("Vultures.Scavengers.of.Death.Update.v1.1.6-TENOKE", "Vultures Scavengers of Death")]
    [TestCase("Shape.of.Dreams.v1.4.0-RUNE", "Shape of Dreams")]
    public void Build_GameVersionMarker_IsClassifiedAsGameWithoutVersionInTitle(
        string releaseName,
        string expectedTitle
    )
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            releaseName,
            [],
            contentKind: null,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Game);
        classification.ContentTypeSource.ShouldBe(ClassificationSource.ReleaseName);
        classification.Title.ShouldBe(expectedTitle);
    }

    [TestCase("Sid_Meiers_Civilization_VII_v1.5.0_Linux-Razor1911", ReleasePlatform.Linux)]
    [TestCase("Pyrga_v1.43_NES-MiRAGE", ReleasePlatform.Nes)]
    public void Build_PlatformTokenInName_IsCarriedToClassification(
        string releaseName,
        ReleasePlatform expectedPlatform
    )
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            releaseName,
            [],
            contentKind: null,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Game);
        classification.Platform.ShouldBe(expectedPlatform);
        classification.PlatformSource.ShouldBe(ClassificationSource.ReleaseName);
    }

    [Test]
    public void Build_GameWithoutPlatformToken_DefaultsToWindows()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Dune.Awakening-RUNE",
            [],
            contentKind: null,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Game);
        classification.Platform.ShouldBe(ReleasePlatform.Windows);
        classification.PlatformSource.ShouldBe(ClassificationSource.None);
    }

    [Test]
    public void Build_ContentKindGame_WinsOverNameHeuristics()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "The.Matrix.1999.1080p.BluRay.x264-GROUP",
            [],
            contentKind: ExternalInfoType.Game,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Game);
        classification.ContentTypeSource.ShouldBe(ClassificationSource.ReleaseInfo);
        classification.Platform.ShouldBe(ReleasePlatform.Windows);
    }

    [Test]
    public void Build_NfoWithSteamAppUrl_IsClassifiedAsGame()
    {
        // Arrange
        var nfoContent = "Release notes\nhttp://store.steampowered.com/app/1172710/\nEnjoy";

        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Dune.Awakening-GRP",
            [],
            contentKind: null,
            nfoContent: nfoContent
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Game);
        classification.ContentTypeSource.ShouldBe(ClassificationSource.Nfo);
    }

    [Test]
    public void Build_NfoWithSteamAppUrlAndContentKindMovie_StaysMovie()
    {
        // Arrange
        var nfoContent = "Release notes\nhttp://store.steampowered.com/app/1172710/\nEnjoy";

        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "The.Matrix.1999.1080p.BluRay.x264-GROUP",
            [],
            contentKind: ExternalInfoType.Movie,
            nfoContent: nfoContent
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Movie);
        classification.ContentTypeSource.ShouldBe(ClassificationSource.ReleaseName);
    }

    [Test]
    public void Build_GameMarkerWithStandaloneYear_IsNotClassifiedAsGame()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Der.Film.Update.2020-GRP",
            [],
            contentKind: null,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldNotBe(ReleaseContentType.Game);
    }

    [Test]
    public void Build_MovieWithResolution_StaysMovieAndHasNoPlatform()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "The.Matrix.1999.1080p.BluRay.x264-GROUP",
            [],
            contentKind: null,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Movie);
        classification.ContentTypeSource.ShouldBe(ClassificationSource.ReleaseName);
        classification.Platform.ShouldBe(ReleasePlatform.Unknown);
        classification.PlatformSource.ShouldBe(ClassificationSource.None);
    }

    [TestCase("The.Switch.2010.German.DL.1080p.BluRay.x264-GROUP", "The Switch")]
    [TestCase("Win.Win.2011.1080p.BluRay.x264-GROUP", "Win Win")]
    public void Build_MovieTitleContainingPlatformWord_KeepsFullTitle(
        string releaseName,
        string expectedTitle
    )
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            releaseName,
            [],
            contentKind: null,
            nfoContent: null
        );

        // Assert
        classification.Title.ShouldBe(expectedTitle);
        classification.ContentType.ShouldBe(ReleaseContentType.Movie);
    }

    [Test]
    public void Build_RepackWithoutResolutionOrSource_IsNotClassifiedAsGame()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Some.Doku.2023.GERMAN.DOKU.REPACK.DL.AC3-GROUP",
            [],
            contentKind: null,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldNotBe(ReleaseContentType.Game);
    }

    [Test]
    public void Build_ContentKindSoftware_SuppressesGameNameHeuristic()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Sandy.Knoll.Software.Metes.and.Bounds.Pro.v6.1-GROUP",
            [],
            contentKind: ExternalInfoType.Software,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldNotBe(ReleaseContentType.Game);
        classification.Platform.ShouldBe(ReleasePlatform.Unknown);
        classification.PlatformSource.ShouldBe(ClassificationSource.None);
    }

    [Test]
    public void Build_ContentKindConsole_IsGameWithoutPlatformAssumption()
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            "Some.Game.Name-GRP",
            [],
            contentKind: ExternalInfoType.Console,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Game);
        classification.ContentTypeSource.ShouldBe(ClassificationSource.ReleaseInfo);
        classification.Platform.ShouldBe(ReleasePlatform.Unknown);
        classification.PlatformSource.ShouldBe(ClassificationSource.None);
    }

    [TestCase("Some.Game.Name.PS5-GRP", ReleasePlatform.PlayStation5)]
    [TestCase("Some.Game.Name.NSW-GRP", ReleasePlatform.NintendoSwitch)]
    public void Build_ContentKindConsoleWithPlatformToken_TakesPlatformFromName(
        string releaseName,
        ReleasePlatform expectedPlatform
    )
    {
        // Act
        var classification = ReleaseClassificationBuilder.Build(
            releaseName,
            [],
            contentKind: ExternalInfoType.Console,
            nfoContent: null
        );

        // Assert
        classification.ContentType.ShouldBe(ReleaseContentType.Game);
        classification.Platform.ShouldBe(expectedPlatform);
        classification.PlatformSource.ShouldBe(ClassificationSource.ReleaseName);
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
