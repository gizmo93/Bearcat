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
        parsed.Source.ShouldBe("BluRay");
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
        parsed.Source.ShouldBe("HDTV");
        parsed.Group.ShouldBe("LOL");
        parsed.Year.ShouldBeNull();
    }

    [Test]
    public void Parse_WebDlWithHyphenSource_KeepsSourceIntact()
    {
        // Act
        var parsed = ReleaseNameParser.Parse("Some.Movie.2023.2160p.WEB-DL.DDP5.1.H.265-GRP");

        // Assert
        parsed.Title.ShouldBe("Some Movie");
        parsed.Year.ShouldBe(2023);
        parsed.Resolution.ShouldBe(ReleaseResolution.R2160p);
        parsed.Source.ShouldBe("WEB-DL");
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
        parsed.Source.ShouldBe("WEB");
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
        parsed.Source.ShouldBe("BluRay");
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
        parsed.Source.ShouldBe("DVDRip");
    }

    [TestCase(
        "Frontier.Crucible.Land.der.Gesetzlosen.2025.German.BDRip.x264-LizardSquad",
        "Frontier Crucible Land der Gesetzlosen",
        "BDRip",
        "LizardSquad"
    )]
    [TestCase("Black.Diamond.2025.German.BDRiP.x264-CPTN", "Black Diamond", "BDRip", "CPTN")]
    [TestCase(
        "Dick.und.Doof.werden.Papa.1936.German.HDTVRip.x264-NORETAiL",
        "Dick und Doof werden Papa",
        "HDTVRip",
        "NORETAiL"
    )]
    public void Parse_SdReleaseWithRipSource_RecognizedWithoutResolution(
        string releaseName,
        string expectedTitle,
        string expectedSource,
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
        parsed.Source.ShouldBeNull();
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
}
