namespace Bearcat.Domain.Shared.Transfers;

public sealed class UserCancellableTransfer : IDisposable
{
    private readonly ITransferCancellationRegistry cancellationRegistry;

    private readonly TransferIdentifier transferIdentifier;

    private readonly CancellationToken userCancellationToken;

    private readonly CancellationTokenSource linkedTokenSource;

    public UserCancellableTransfer(
        ITransferCancellationRegistry cancellationRegistry,
        TransferIdentifier transferIdentifier,
        CancellationToken cancellationToken
    )
    {
        this.cancellationRegistry = cancellationRegistry;
        this.transferIdentifier = transferIdentifier;
        userCancellationToken = cancellationRegistry.Register(transferIdentifier);
        linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            userCancellationToken
        );
    }

    public CancellationToken CancellationToken => linkedTokenSource.Token;

    public bool WasCanceledByUser => userCancellationToken.IsCancellationRequested;

    public void Dispose()
    {
        linkedTokenSource.Dispose();
        cancellationRegistry.Unregister(transferIdentifier);
    }
}
