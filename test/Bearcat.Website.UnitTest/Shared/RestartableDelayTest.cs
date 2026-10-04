using Bearcat.Website.Shared;
using Shouldly;

namespace Bearcat.Website.UnitTest.Shared;

public class RestartableDelayTest
{
    [Test]
    public async Task WaitAsync_NotRestarted_ReturnsTrue()
    {
        // Arrange
        using var delay = new RestartableDelay();

        // Act
        var completed = await delay.WaitAsync(TimeSpan.FromMilliseconds(1));

        // Assert
        completed.ShouldBeTrue();
    }

    [Test]
    public async Task WaitAsync_RestartedBeforeDelayElapsed_FirstWaitReturnsFalse()
    {
        // Arrange
        using var delay = new RestartableDelay();
        var firstWait = delay.WaitAsync(TimeSpan.FromMinutes(1));

        // Act
        var secondWait = delay.WaitAsync(TimeSpan.FromMilliseconds(1));

        // Assert
        (await firstWait).ShouldBeFalse();
        (await secondWait).ShouldBeTrue();
    }

    [Test]
    public async Task Cancel_PendingWait_ReturnsFalse()
    {
        // Arrange
        using var delay = new RestartableDelay();
        var wait = delay.WaitAsync(TimeSpan.FromMinutes(1));

        // Act
        delay.Cancel();

        // Assert
        (await wait).ShouldBeFalse();
    }
}
