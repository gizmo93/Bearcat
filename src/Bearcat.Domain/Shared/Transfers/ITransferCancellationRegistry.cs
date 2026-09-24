namespace Bearcat.Domain.Shared.Transfers;

public interface ITransferCancellationRegistry
{
    CancellationToken Register(TransferIdentifier identifier);

    void Unregister(TransferIdentifier identifier);

    bool RequestCancellation(TransferIdentifier identifier);
}
