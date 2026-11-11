using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseCollections;

public class ReleaseCollectionDetectionServiceTest
{
    [Test]
    public void Detect_SeriesEpisodePattern_GroupsSeasonRelease()
    {
        // Arrange
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.SeriesEpisodePattern,
        };

        // Act
        var result = ReleaseCollectionDetectionService.Detect(
            "Hostage.S01E01.German.AC3.DL.1080p.Web.x265-FuN.mkv",
            releaseTemplate
        );

        // Assert
        result.ShouldNotBeNull();
        result.Key.ShouldBe("hostage.s01.german.ac3.dl.1080p.web.x265.fun.mkv");
        result.Name.ShouldBe("Hostage.S01.German.AC3.DL.1080p.Web.x265-FuN.mkv");
    }

    [TestCase(
        "Penny.Dreadful.S03E07.Welt.der.Finsternis.German.DL.1080p.BluRay.x264-RSG",
        "Penny.Dreadful.S03.German.DL.1080p.BluRay.x264-RSG"
    )]
    [TestCase(
        "Penny.Dreadful.S03E08.Ewige.Nacht.German.DL.1080p.BluRay.x264-RSG",
        "Penny.Dreadful.S03.German.DL.1080p.BluRay.x264-RSG"
    )]
    [TestCase(
        "Severance.S02E04.Woes.Hollow.2160p.ATVP.WEB-DL.DDP5.1.H.265-NTb",
        "Severance.S02.2160p.ATVP.WEB-DL.DDP5.1.H.265-NTb"
    )]
    [TestCase(
        "Some.Show.S01E05.Episode.Title.REPACK.720p.HDTV.x264-GRP",
        "Some.Show.S01.REPACK.720p.HDTV.x264-GRP"
    )]
    [TestCase(
        "Some.Show.S01E01.UNCUT.German.DL.1080p.BluRay.x264-GRP",
        "Some.Show.S01.UNCUT.German.DL.1080p.BluRay.x264-GRP"
    )]
    [TestCase("Some.Show.S01E02.Episode.Title.XviD-GRP", "Some.Show.S01.Episode.Title.XviD-GRP")]
    public void Detect_SeriesEpisodePatternWithEpisodeTitle_RemovesEpisodeTitleBeforeReleaseTags(
        string releaseName,
        string expectedCollectionName
    )
    {
        // Arrange
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseCollectionDetectionMode =
                ReleaseCollectionDetectionMode.SeriesEpisodePatternWithEpisodeTitle,
        };

        // Act
        var result = ReleaseCollectionDetectionService.Detect(releaseName, releaseTemplate);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(expectedCollectionName);
    }

    [Test]
    public void Detect_SeriesEpisodePattern_EpisodeTitleBeforeReleaseTags_KeepsEpisodeTitle()
    {
        // Arrange
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.SeriesEpisodePattern,
        };

        // Act
        var result = ReleaseCollectionDetectionService.Detect(
            "Penny.Dreadful.S03E07.Welt.der.Finsternis.German.DL.1080p.BluRay.x264-RSG",
            releaseTemplate
        );

        // Assert
        result.ShouldNotBeNull();

        result.Name.ShouldBe(
            "Penny.Dreadful.S03.Welt.der.Finsternis.German.DL.1080p.BluRay.x264-RSG"
        );
    }

    [Test]
    public void Detect_DisabledMode_ReturnsNull()
    {
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.Disabled,
        };

        var result = ReleaseCollectionDetectionService.Detect(
            "Hostage.S01E01.German.AC3.DL.1080p.Web.x265-FuN.mkv",
            releaseTemplate
        );

        result.ShouldBeNull();
    }

    [Test]
    public void Detect_SeriesEpisodePattern_NoEpisodeMarker_ReturnsNull()
    {
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.SeriesEpisodePattern,
        };

        var result = ReleaseCollectionDetectionService.Detect(
            "Hostage.German.BluRay.1080p.x265-FuN.mkv",
            releaseTemplate
        );

        result.ShouldBeNull();
    }

    [Test]
    public void Detect_SeriesEpisodePattern_CustomTemplates_UsesCustomKeyAndName()
    {
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            ReleaseCollectionKeyTemplate = "{title:dotToSpace}.s{season}",
            ReleaseCollectionNameTemplate = "{title:dotToSpace} S{season}",
        };

        var result = ReleaseCollectionDetectionService.Detect(
            "Dark.S02E05.German.DL.1080p.BluRay.x265-FuN",
            releaseTemplate
        );

        result.ShouldNotBeNull();
        result.Key.ShouldBe("dark.s02");
        result.Name.ShouldBe("Dark S02");
    }

    [Test]
    public void Detect_CustomRegex_MatchingRelease_ReturnsExpectedKeyAndName()
    {
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.CustomRegex,
            ReleaseCollectionPattern = @"^(?<title>.+?)\.(?<year>\d{4})\.German",
            ReleaseCollectionKeyTemplate = "{title}.{year}",
            ReleaseCollectionNameTemplate = "{title:dotToSpace} ({year})",
        };

        var result = ReleaseCollectionDetectionService.Detect(
            "The.Dark.Knight.2008.German.DL.1080p.BluRay.x265-FuN",
            releaseTemplate
        );

        result.ShouldNotBeNull();
        result.Key.ShouldBe("the.dark.knight.2008");
        result.Name.ShouldBe("The Dark Knight (2008)");
    }

    [Test]
    public void Detect_CustomRegex_PatternNotConfigured_ReturnsNull()
    {
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.CustomRegex,
            ReleaseCollectionPattern = null,
            ReleaseCollectionKeyTemplate = "{title}",
            ReleaseCollectionNameTemplate = "{title}",
        };

        var result = ReleaseCollectionDetectionService.Detect(
            "Hostage.S01E01.German.AC3.DL.1080p.Web.x265-FuN.mkv",
            releaseTemplate
        );

        result.ShouldBeNull();
    }

    [Test]
    public void Detect_CustomRegex_NonMatchingRelease_ReturnsNull()
    {
        var releaseTemplate = new ReleaseTemplate
        {
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.CustomRegex,
            ReleaseCollectionPattern = @"^(?<title>.+?)\.S\d{2}E\d{2}",
            ReleaseCollectionKeyTemplate = "{title}",
            ReleaseCollectionNameTemplate = "{title}",
        };

        var result = ReleaseCollectionDetectionService.Detect(
            "The.Dark.Knight.2008.German.DL.1080p.BluRay.x265-FuN",
            releaseTemplate
        );

        result.ShouldBeNull();
    }
}
