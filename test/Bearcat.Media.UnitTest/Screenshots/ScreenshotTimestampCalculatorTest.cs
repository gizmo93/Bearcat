using Bearcat.Media.Screenshots;
using Shouldly;

namespace Bearcat.Media.UnitTest.Screenshots;

public class ScreenshotTimestampCalculatorTest
{
    [Test]
    public void Calculate_SingleScreenshot_ReturnsMiddleOfDuration()
    {
        // Act
        var timestamps = ScreenshotTimestampCalculator.Calculate(TimeSpan.FromMinutes(10), 1);

        // Assert
        timestamps.ShouldBe([TimeSpan.FromMinutes(5)]);
    }

    [Test]
    public void Calculate_MultipleScreenshots_SpreadsSlotMiddlesBetweenFiveAndNinetyFivePercent()
    {
        // Act
        var timestamps = ScreenshotTimestampCalculator.Calculate(TimeSpan.FromSeconds(100), 4);

        // Assert
        timestamps.ShouldBe([
            TimeSpan.FromSeconds(16.25),
            TimeSpan.FromSeconds(38.75),
            TimeSpan.FromSeconds(61.25),
            TimeSpan.FromSeconds(83.75),
        ]);
    }
}
