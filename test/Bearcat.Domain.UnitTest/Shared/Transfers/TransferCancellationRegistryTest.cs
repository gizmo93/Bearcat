using Bearcat.Domain.Shared.Transfers;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.Transfers;

public class TransferCancellationRegistryTest
{
    [Test]
    public void RequestCancellation_NothingRegistered_ReturnsFalse()
    {
        // Arrange
        var registry = new TransferCancellationRegistry();

        // Act
        var result = registry.RequestCancellation(
            new TransferIdentifier(TransferKind.MirrorDownload, 4711)
        );

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void RequestCancellation_KeyIsRegistered_CancelsTheTokenOnce()
    {
        // Arrange
        var registry = new TransferCancellationRegistry();
        var key = new TransferIdentifier(TransferKind.MirrorDownload, 3);
        var token = registry.Register(key);

        // Act
        var firstResult = registry.RequestCancellation(key);
        registry.Unregister(key);
        var secondResult = registry.RequestCancellation(key);

        // Assert
        firstResult.ShouldBeTrue();
        token.IsCancellationRequested.ShouldBeTrue();
        secondResult.ShouldBeFalse();
    }

    [Test]
    public void RequestCancellation_SameIdWithDifferentKind_DoesNotCancel()
    {
        // Arrange
        var registry = new TransferCancellationRegistry();
        var token = registry.Register(new TransferIdentifier(TransferKind.MirrorDownload, 3));

        // Act
        var result = registry.RequestCancellation(new TransferIdentifier(TransferKind.Upload, 3));

        // Assert
        result.ShouldBeFalse();
        token.IsCancellationRequested.ShouldBeFalse();
    }
}
