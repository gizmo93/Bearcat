using Bearcat.Website.Pages.Home.Summary;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.Home.Summary;

public class SparklinePointsBuilderTest
{
    [Test]
    public void Build_FewerThanTwoSamples_ReturnsNull()
    {
        // Act
        var points = SparklinePointsBuilder.Build([42], capacity: 5);

        // Assert
        points.ShouldBeNull();
    }

    [Test]
    public void Build_SamplesFillCapacity_SpreadsPointsOverFullWidthScaledToMaximum()
    {
        // Act
        var points = SparklinePointsBuilder.Build([0, 50, 100, 50, 0], capacity: 5);

        // Assert
        points.ShouldNotBeNull();
        points.LinePoints.ShouldBe("0,30 25,16 50,2 75,16 100,30");
        points.AreaPoints.ShouldBe("0,30 25,16 50,2 75,16 100,30 100,32 0,32");
    }

    [Test]
    public void Build_FewerSamplesThanCapacity_AlignsNewestSampleToRightEdge()
    {
        // Act
        var points = SparklinePointsBuilder.Build([0, 50, 100], capacity: 5);

        // Assert
        points.ShouldNotBeNull();
        points.LinePoints.ShouldBe("50,30 75,16 100,2");
        points.AreaPoints.ShouldBe("50,30 75,16 100,2 100,32 50,32");
    }

    [Test]
    public void Build_AllSamplesZero_DrawsBaseline()
    {
        // Act
        var points = SparklinePointsBuilder.Build([0, 0, 0], capacity: 3);

        // Assert
        points.ShouldNotBeNull();
        points.LinePoints.ShouldBe("0,30 50,30 100,30");
    }
}
