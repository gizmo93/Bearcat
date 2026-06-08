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
