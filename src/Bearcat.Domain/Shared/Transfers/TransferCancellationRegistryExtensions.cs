namespace Bearcat.Domain.Shared.Transfers;

public static class TransferCancellationRegistryExtensions
{
    public static UserCancellableTransfer RegisterUserCancellableTransfer(
        this ITransferCancellationRegistry cancellationRegistry,
        TransferIdentifier transferIdentifier,
        CancellationToken cancellationToken
    )
    {
        return new UserCancellableTransfer(
            cancellationRegistry,
            transferIdentifier,
            cancellationToken
        );
    }
}
