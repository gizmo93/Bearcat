using Bearcat.Website.Pages.ManageReleaseCollections.Overview;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleaseCollections.Overview;

public class ReleaseCollectionEpisodeLabelTest
{
    [Test]
    public void GetEpisodeLabels_AllReleasesInSameSeason_ReturnsEpisodeOnlyLabels()
    {
        // Act
        var labels = ReleaseCollectionEpisodeLabel.GetEpisodeLabels([
            "Hostage.S01E01.German.1080p-GROUP",
            "Hostage.S01E02.German.1080p-GROUP",
        ]);

        // Assert
        labels.ShouldBe(["E01", "E02"]);
    }

    [Test]
    public void GetEpisodeLabels_ReleasesInDifferentSeasons_ReturnsSeasonAndEpisodeLabels()
    {
        // Act
        var labels = ReleaseCollectionEpisodeLabel.GetEpisodeLabels([
            "Hostage.S01E10.German.1080p-GROUP",
            "Hostage.S02E01.German.1080p-GROUP",
        ]);

        // Assert
        labels.ShouldBe(["S01E10", "S02E01"]);
    }

    [Test]
    public void GetEpisodeLabels_LowercaseSeasonAndEpisode_ReturnsUppercaseLabelWithDigitsAsWritten()
    {
        // Act
        var labels = ReleaseCollectionEpisodeLabel.GetEpisodeLabels([
            "hostage.s1e5.german-group",
            "hostage.s01e0006.german-group",
        ]);

        // Assert
        labels.ShouldBe(["E5", "E0006"]);
    }

    [Test]
    public void GetEpisodeLabels_NameWithoutSeasonAndEpisode_ReturnsNullAndIgnoresItForSeasonComparison()
    {
        // Act
        var labels = ReleaseCollectionEpisodeLabel.GetEpisodeLabels([
            "Hostage.S03E01.German-GROUP",
            "Hostage.Complete.Special.German-GROUP",
        ]);

        // Assert
        labels.ShouldBe(["E01", null]);
    }

    [Test]
    public void GetEpisodeLabels_NoReleases_ReturnsEmptyList()
    {
        // Act
        var labels = ReleaseCollectionEpisodeLabel.GetEpisodeLabels([]);

        // Assert
        labels.ShouldBeEmpty();
    }
}
