using Bearcat.Website.Pages.Home.Summary;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.Home.Summary;

public class TransferSpeedHistoryTest
{
    [Test]
    public void Add_FewerSamplesThanCapacity_KeepsAllSamplesInOrder()
    {
        // Arrange
        var history = new TransferSpeedHistory(capacity: 3);

        // Act
        history.Add(10);
        history.Add(20);

        // Assert
        history.Samples.ShouldBe([10, 20]);
    }

    [Test]
    public void Add_MoreSamplesThanCapacity_DropsOldestSamples()
    {
        // Arrange
        var history = new TransferSpeedHistory(capacity: 3);

        // Act
        history.Add(10);
        history.Add(20);
        history.Add(30);
        history.Add(40);
        history.Add(50);

        // Assert
        history.Samples.ShouldBe([30, 40, 50]);
    }
}
