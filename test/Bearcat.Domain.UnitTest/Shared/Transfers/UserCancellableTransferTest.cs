using Bearcat.Domain.Shared.Transfers;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.Transfers;

public class UserCancellableTransferTest
{
    [Test]
    public void CancellationToken_UserRequestsCancellation_IsCanceledAndWasCanceledByUser()
    {
        // Arrange
        var registry = new TransferCancellationRegistry();
        var transferIdentifier = new TransferIdentifier(TransferType.MirrorDownload, 3);
        using var userCancellableTransfer = registry.RegisterUserCancellableTransfer(
            transferIdentifier,
            CancellationToken.None
        );

        // Act
        registry.RequestCancellation(transferIdentifier);

        // Assert
        userCancellableTransfer.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        userCancellableTransfer.WasCanceledByUser.ShouldBeTrue();
    }

    [Test]
    public void CancellationToken_CallerTokenIsCanceled_IsCanceledButNotByUser()
    {
        // Arrange
        var registry = new TransferCancellationRegistry();
        using var callerTokenSource = new CancellationTokenSource();
        using var userCancellableTransfer = registry.RegisterUserCancellableTransfer(
            new TransferIdentifier(TransferType.MirrorDownload, 3),
            callerTokenSource.Token
        );

        // Act
        callerTokenSource.Cancel();

        // Assert
        userCancellableTransfer.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        userCancellableTransfer.WasCanceledByUser.ShouldBeFalse();
    }

    [Test]
    public void Dispose_TransferIsRegistered_UnregistersTheTransfer()
    {
        // Arrange
        var registry = new TransferCancellationRegistry();
        var transferIdentifier = new TransferIdentifier(TransferType.MirrorDownload, 3);
        var userCancellableTransfer = registry.RegisterUserCancellableTransfer(
            transferIdentifier,
            CancellationToken.None
        );

        // Act
        userCancellableTransfer.Dispose();

        // Assert
        registry.RequestCancellation(transferIdentifier).ShouldBeFalse();
    }
}
