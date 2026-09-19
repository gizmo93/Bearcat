using Bearcat.Domain.UseCases.ManageReleases.ReleaseNameParsing;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageReleases.ReleaseNameParsing;

public class ReleaseNameParserTest
{
    [Test]
    public void Parse_Movie_ExtractsTitleYearResolutionSourceGroup()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("The.Matrix.1999.1080p.BluRay.x264-GROUP");

        // Assert
        parsed.Title.ShouldBe("The Matrix");
        parsed.Year.ShouldBe(1999);
        parsed.Resolution.ShouldBe(ReleaseResolution.R1080p);
        parsed.Source.ShouldBe(ReleaseSource.BluRay);
        parsed.Group.ShouldBe("GROUP");
        parsed.Season.ShouldBeNull();
        parsed.Episode.ShouldBeNull();
        parsed.Language.ShouldBeNull();
    }

    [Test]
    public void Parse_TvEpisode_ExtractsSeasonAndEpisode()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Breaking.Bad.S03E10.720p.HDTV.x264-LOL");

        // Assert
        parsed.Title.ShouldBe("Breaking Bad");
        parsed.Season.ShouldBe(3);
        parsed.Episode.ShouldBe(10);
        parsed.EpisodeEnd.ShouldBeNull();
        parsed.Resolution.ShouldBe(ReleaseResolution.R720p);
        parsed.Source.ShouldBe(ReleaseSource.Hdtv);
        parsed.Group.ShouldBe("LOL");
        parsed.Year.ShouldBeNull();
    }

    [Test]
    public void Parse_WebDlWithHyphenSource_MapsToWebDlSource()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Some.Movie.2023.2160p.WEB-DL.DDP5.1.H.265-GRP");

        // Assert
        parsed.Title.ShouldBe("Some Movie");
        parsed.Year.ShouldBe(2023);
        parsed.Resolution.ShouldBe(ReleaseResolution.R2160p);
        parsed.Source.ShouldBe(ReleaseSource.WebDl);
        parsed.Group.ShouldBe("GRP");
    }

    [Test]
    public void Parse_GermanDualLanguageSeries_ExtractsLanguageAndMultiFlag()
    {
        // Act
        var parsed = ReleaseNameParser.Parse(
            "Bodies.2023.S01E01.German.DL.EAC3.1080p.DV.HDR.NF.WEB.H265-ZeroTwo"
        );

        // Assert
        parsed.Title.ShouldBe("Bodies");
        parsed.Year.ShouldBe(2023);
        parsed.Season.ShouldBe(1);
        parsed.Episode.ShouldBe(1);
        parsed.Language.ShouldBe("German");
        parsed.IsMultiLanguage.ShouldBeTrue();
        parsed.Resolution.ShouldBe(ReleaseResolution.R1080p);
        parsed.Source.ShouldBe(ReleaseSource.Web);
        parsed.Group.ShouldBe("ZeroTwo");
    }

    [Test]
    public void Parse_GermanMovie_ExtractsLanguage()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Amok.1994.German.1080p.BluRay.x264-PL3X");

        // Assert
        parsed.Title.ShouldBe("Amok");
        parsed.Year.ShouldBe(1994);
        parsed.Language.ShouldBe("German");
        parsed.IsMultiLanguage.ShouldBeFalse();
        parsed.Resolution.ShouldBe(ReleaseResolution.R1080p);
        parsed.Source.ShouldBe(ReleaseSource.BluRay);
        parsed.Group.ShouldBe("PL3X");
    }

    [TestCase("Amok.1994.Deutsch.1080p.BluRay-GRP", "German")]
    [TestCase("Some.Movie.2020.French.1080p.BluRay-GRP", "French")]
    [TestCase("Some.Movie.2020.Polish.1080p.BluRay-GRP", "Polish")]
    [TestCase("Some.Movie.2020.ITA.1080p.BluRay-GRP", "Italian")]
    public void Parse_LanguageVariants_NormalizeToEnglishName(
        string releaseName,
        string expectedLanguage
    )
    {
        // Act
        var parsed = ReleaseNameParser.Parse(releaseName);

        // Assert
        parsed.Language.ShouldBe(expectedLanguage);
    }

    [Test]
    public void Parse_TitleWordMatchingThreeLetterCode_IsNotTreatedAsLanguage()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Breaking.Bad.S03E10.720p.HDTV.x264-LOL");

        // Assert
        parsed.Title.ShouldBe("Breaking Bad");
        parsed.Language.ShouldBeNull();
    }

    [Test]
    public void Parse_MultiTokenTitleWithNumber_KeepsNumberInTitle()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Moon.44.1990.German.DL.1080p.BluRay.x264-iFPD");

        // Assert
        parsed.Title.ShouldBe("Moon 44");
        parsed.Year.ShouldBe(1990);
        parsed.Language.ShouldBe("German");
        parsed.IsMultiLanguage.ShouldBeTrue();
        parsed.Resolution.ShouldBe(ReleaseResolution.R1080p);
    }

    [Test]
    public void Parse_SdReleaseWithoutResolution_LeavesResolutionUnknown()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Some.Old.Movie.1985.German.DVDRip.x264-GRP");

        // Assert
        parsed.Title.ShouldBe("Some Old Movie");
        parsed.Year.ShouldBe(1985);
        parsed.Resolution.ShouldBe(ReleaseResolution.Unknown);
        parsed.Source.ShouldBe(ReleaseSource.DvdRip);
    }

    [TestCase(
        "Frontier.Crucible.Land.der.Gesetzlosen.2025.German.BDRip.x264-LizardSquad",
        "Frontier Crucible Land der Gesetzlosen",
        ReleaseSource.BdRip,
        "LizardSquad"
    )]
    [TestCase(
        "Black.Diamond.2025.German.BDRiP.x264-CPTN",
        "Black Diamond",
        ReleaseSource.BdRip,
        "CPTN"
    )]
    [TestCase(
        "Dick.und.Doof.werden.Papa.1936.German.HDTVRip.x264-NORETAiL",
        "Dick und Doof werden Papa",
        ReleaseSource.HdtvRip,
        "NORETAiL"
    )]
    public void Parse_SdReleaseWithRipSource_RecognizedWithoutResolution(
        string releaseName,
        string expectedTitle,
        ReleaseSource expectedSource,
        string expectedGroup
    )
    {
        // Act
        var parsed = ReleaseNameParser.Parse(releaseName);

        // Assert
        parsed.Title.ShouldBe(expectedTitle);
        parsed.Language.ShouldBe("German");
        parsed.Resolution.ShouldBe(ReleaseResolution.Unknown);
        parsed.Source.ShouldBe(expectedSource);
        parsed.Group.ShouldBe(expectedGroup);
        parsed.LooksLikeReleaseName.ShouldBeTrue();
    }

    [Test]
    public void Parse_SeasonPackWithoutEpisode_ExtractsSeasonOnly()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Breaking.Bad.S03.1080p.BluRay.x264-GROUP");

        // Assert
        parsed.Season.ShouldBe(3);
        parsed.Episode.ShouldBeNull();
        parsed.Title.ShouldBe("Breaking Bad");
    }

    [Test]
    public void Parse_MultiEpisode_ExtractsEpisodeRange()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Some.Show.S01E01E02.1080p.WEB-DL-GRP");

        // Assert
        parsed.Season.ShouldBe(1);
        parsed.Episode.ShouldBe(1);
        parsed.EpisodeEnd.ShouldBe(2);
    }

    [Test]
    public void Parse_ReleaseName_LooksLikeReleaseName()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("The.Matrix.1999.1080p.BluRay.x264-GROUP");

        // Assert
        parsed.LooksLikeReleaseName.ShouldBeTrue();
    }

    [Test]
    public void Parse_ReleaseWithoutResolutionOrSource_StillLooksLikeReleaseName()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Some.Movie.2024.German-GROUP");

        // Assert
        parsed.Title.ShouldBe("Some Movie");
        parsed.Year.ShouldBe(2024);
        parsed.Language.ShouldBe("German");
        parsed.Resolution.ShouldBe(ReleaseResolution.Unknown);
        parsed.Source.ShouldBe(ReleaseSource.Unknown);
        parsed.LooksLikeReleaseName.ShouldBeTrue();
    }

    [Test]
    public void Parse_PlainFolderName_DoesNotLookLikeReleaseName()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("My Home Videos 2019");

        // Assert
        parsed.LooksLikeReleaseName.ShouldBeFalse();
        parsed.Group.ShouldBeNull();
    }

    [TestCase("Some.Movie.2023.1080p.BluRay.x264-GRP", ReleaseSource.BluRay)]
    [TestCase("Some.Movie.2023.1080p.BDRip.x264-GRP", ReleaseSource.BdRip)]
    [TestCase("Some.Movie.2023.1080p.BRRip.x264-GRP", ReleaseSource.BrRip)]
    [TestCase("Some.Movie.2023.1080p.WEB-DL.x264-GRP", ReleaseSource.WebDl)]
    [TestCase("Some.Movie.2023.1080p.WEBDL.x264-GRP", ReleaseSource.WebDl)]
    [TestCase("Some.Movie.2023.1080p.WEB.x264-GRP", ReleaseSource.Web)]
    [TestCase("Some.Movie.2023.1080p.WEBRip.x264-GRP", ReleaseSource.WebRip)]
    [TestCase("Some.Movie.2023.1080p.HDTV.x264-GRP", ReleaseSource.Hdtv)]
    [TestCase("Some.Movie.2023.HDTVRip.x264-GRP", ReleaseSource.HdtvRip)]
    [TestCase("Some.Movie.2023.PDTV.x264-GRP", ReleaseSource.Pdtv)]
    [TestCase("Some.Movie.2023.DVDRip.x264-GRP", ReleaseSource.DvdRip)]
    [TestCase("Some.Movie.2023.DVDR.x264-GRP", ReleaseSource.DvdR)]
    [TestCase("Some.Movie.2023.HDRip.x264-GRP", ReleaseSource.HdRip)]
    [TestCase("Some.Movie.2023.TVRip.x264-GRP", ReleaseSource.TvRip)]
    [TestCase("Some.Movie.2023.SATRip.x264-GRP", ReleaseSource.SatRip)]
    public void Parse_SourceToken_MapsToReleaseSource(
        string releaseName,
        ReleaseSource expectedSource
    )
    {
        // Act
        var parsed = ReleaseNameParser.Parse(releaseName);

        // Assert
        parsed.Source.ShouldBe(expectedSource);
    }

    [Test]
    public void Parse_GermanDualLanguageMovie_ExtractsSourceAndGroup()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Movie.Name.2023.German.DL.1080p.BluRay.x264-GROUP");

        // Assert
        parsed.Title.ShouldBe("Movie Name");
        parsed.Year.ShouldBe(2023);
        parsed.Language.ShouldBe("German");
        parsed.IsMultiLanguage.ShouldBeTrue();
        parsed.Resolution.ShouldBe(ReleaseResolution.R1080p);
        parsed.Source.ShouldBe(ReleaseSource.BluRay);
        parsed.Group.ShouldBe("GROUP");
        parsed.Season.ShouldBeNull();
        parsed.Episode.ShouldBeNull();
    }

    [Test]
    public void Parse_GermanEpisode_ExtractsSeasonEpisodeSourceAndGroup()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Show.Name.S01E03.German.720p.WEB.h264-GRP");

        // Assert
        parsed.Title.ShouldBe("Show Name");
        parsed.Season.ShouldBe(1);
        parsed.Episode.ShouldBe(3);
        parsed.EpisodeEnd.ShouldBeNull();
        parsed.Language.ShouldBe("German");
        parsed.Resolution.ShouldBe(ReleaseResolution.R720p);
        parsed.Source.ShouldBe(ReleaseSource.Web);
        parsed.Group.ShouldBe("GRP");
    }

    [Test]
    public void Parse_GermanMultiEpisode_ExtractsEpisodeEnd()
    {
        // Act
        var parsed = ReleaseNameParser.Parse(
            "Show.Name.S02E05-E06.German.DL.1080p.WEB-DL.x264-TVS"
        );

        // Assert
        parsed.Season.ShouldBe(2);
        parsed.Episode.ShouldBe(5);
        parsed.EpisodeEnd.ShouldBe(6);
        parsed.Source.ShouldBe(ReleaseSource.WebDl);
        parsed.Group.ShouldBe("TVS");
    }

    [Test]
    public void Parse_MultiWithLanguageCount_IsDetectedAsMultiLanguage()
    {
        // Act
        var parsed = ReleaseNameParser.Parse(
            "Assassins.Creed.Mirage.Master.Assassin.Edition.MULTi14-ElAmigos"
        );

        // Assert
        parsed.Title.ShouldBe("Assassins Creed Mirage Master Assassin Edition");
        parsed.IsMultiLanguage.ShouldBeTrue();
        parsed.Group.ShouldBe("ElAmigos");
    }

    [TestCase("Sid_Meiers_Civilization_VII_v1.5.0_Linux-Razor1911", ReleasePlatform.Linux)]
    [TestCase("Pyrga_v1.43_NES-MiRAGE", ReleasePlatform.Nes)]
    [TestCase("Some.Game.v1.0.MacOS-GRP", ReleasePlatform.MacOs)]
    [TestCase("Some.Game.v1.0.PS5-GRP", ReleasePlatform.PlayStation5)]
    public void Parse_PlatformToken_MapsToReleasePlatform(
        string releaseName,
        ReleasePlatform expectedPlatform
    )
    {
        // Act
        var parsed = ReleaseNameParser.Parse(releaseName);

        // Assert
        parsed.Platform.ShouldBe(expectedPlatform);
    }

    [Test]
    public void Parse_GameWithVersion_StopsTitleAtVersionToken()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Sid_Meiers_Civilization_VII_v1.5.0_Linux-Razor1911");

        // Assert
        parsed.Title.ShouldBe("Sid Meiers Civilization VII");
        parsed.HasGameMarkers.ShouldBeTrue();
        parsed.Platform.ShouldBe(ReleasePlatform.Linux);
        parsed.Group.ShouldBe("Razor1911");
    }

    [TestCase("Vultures.Scavengers.of.Death.Update.v1.1.6-TENOKE", "Vultures Scavengers of Death")]
    [TestCase("Shape.of.Dreams.v1.4.0-RUNE", "Shape of Dreams")]
    [TestCase("ChainStaff_Time_Trials_Plus_8_Trainer-RazorDOX", "ChainStaff Time Trials Plus 8")]
    [TestCase(
        "Firefighting.Simulator.Ignite.Motor.Vehicle.Accident.Update.v1.0070.incl.DLC-RUNE",
        "Firefighting Simulator Ignite Motor Vehicle Accident"
    )]
    public void Parse_GameMarker_IsDetectedAndEndsTitle(string releaseName, string expectedTitle)
    {
        // Act
        var parsed = ReleaseNameParser.Parse(releaseName);

        // Assert
        parsed.HasGameMarkers.ShouldBeTrue();
        parsed.Title.ShouldBe(expectedTitle);
    }

    [Test]
    public void Parse_GameMarkerWordInMovieName_DoesNotTruncateTitle()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Status.Update.2018.1080p.WEB.h264-GRP");

        // Assert
        parsed.Title.ShouldBe("Status Update");
        parsed.Year.ShouldBe(2018);
    }

    [Test]
    public void Parse_MovieWithoutGameMarkers_HasNoGameMarkersAndNoPlatform()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("The.Matrix.1999.1080p.BluRay.x264-GROUP");

        // Assert
        parsed.HasGameMarkers.ShouldBeFalse();
        parsed.Platform.ShouldBe(ReleasePlatform.Unknown);
    }
}
