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
            UseReleaseCollections = true,
            ReleaseCollectionDetectionMode = ReleaseCollectionDetectionMode.SeriesEpisodePattern,
            IgnoreLanguageInReleaseCollectionName = true,
        };

        // Act
        var result = ReleaseCollectionDetectionService.Detect(
            "Hostage.S01E01.German.AC3.DL.1080p.Web.x265-FuN.mkv",
            releaseTemplate
        );

        // Assert
        result.ShouldNotBeNull();
        result.Key.ShouldBe("hostage.s01.ac3.dl.1080p.web.x265.fun");
        result.Name.ShouldBe("Hostage S01 AC3.DL.1080p.Web.x265-FuN");
    }
}
