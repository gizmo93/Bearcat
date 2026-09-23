using Bearcat.Abstractions;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Stability;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.Stability;

public class FolderStabilityGateTest
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Local);

    private static readonly TimeSpan StabilityWindow = TimeSpan.FromMinutes(5);

    [Test]
    public void Evaluate_FingerprintChanged_ReturnsChanged()
    {
        // Act
        var stability = FolderStabilityGate.Evaluate(
            observed: new FolderContentFingerprint(3, 1000),
            current: new FolderContentFingerprint(4, 1500),
            lastChangedAt: Now.AddHours(-1),
            now: Now,
            stabilityWindow: StabilityWindow
        );

        // Assert
        stability.ShouldBe(FolderStability.Changed);
    }

    [Test]
    public void Evaluate_UnchangedWithinWindow_ReturnsSettling()
    {
        // Act
        var stability = FolderStabilityGate.Evaluate(
            observed: new FolderContentFingerprint(3, 1000),
            current: new FolderContentFingerprint(3, 1000),
            lastChangedAt: Now.AddMinutes(-4),
            now: Now,
            stabilityWindow: StabilityWindow
        );

        // Assert
        stability.ShouldBe(FolderStability.Settling);
    }

    [Test]
    public void Evaluate_UnchangedForWholeWindow_ReturnsStable()
    {
        // Act
        var stability = FolderStabilityGate.Evaluate(
            observed: new FolderContentFingerprint(3, 1000),
            current: new FolderContentFingerprint(3, 1000),
            lastChangedAt: Now.AddMinutes(-5),
            now: Now,
            stabilityWindow: StabilityWindow
        );

        // Assert
        stability.ShouldBe(FolderStability.Stable);
    }
}
