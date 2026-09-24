using Bearcat.Abstractions;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Stability;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.Stability;

public class FolderStabilityCheckTest
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Local);

    private static readonly TimeSpan StabilityWindow = TimeSpan.FromMinutes(5);

    [Test]
    public void GetStability_FingerprintChanged_ReturnsChanged()
    {
        // Act
        var stability = FolderStabilityCheck.GetStability(
            observed: new FolderFileCountAndSize(3, 1000),
            current: new FolderFileCountAndSize(4, 1500),
            lastChangedAt: Now.AddHours(-1),
            now: Now,
            stabilityWindow: StabilityWindow
        );

        // Assert
        stability.ShouldBe(FolderStability.Changed);
    }

    [Test]
    public void GetStability_UnchangedWithinWindow_ReturnsSettling()
    {
        // Act
        var stability = FolderStabilityCheck.GetStability(
            observed: new FolderFileCountAndSize(3, 1000),
            current: new FolderFileCountAndSize(3, 1000),
            lastChangedAt: Now.AddMinutes(-4),
            now: Now,
            stabilityWindow: StabilityWindow
        );

        // Assert
        stability.ShouldBe(FolderStability.NotYetStable);
    }

    [Test]
    public void GetStability_UnchangedForWholeWindow_ReturnsStable()
    {
        // Act
        var stability = FolderStabilityCheck.GetStability(
            observed: new FolderFileCountAndSize(3, 1000),
            current: new FolderFileCountAndSize(3, 1000),
            lastChangedAt: Now.AddMinutes(-5),
            now: Now,
            stabilityWindow: StabilityWindow
        );

        // Assert
        stability.ShouldBe(FolderStability.Stable);
    }
}
