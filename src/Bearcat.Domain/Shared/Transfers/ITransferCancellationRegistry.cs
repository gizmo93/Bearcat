namespace Bearcat.Domain.Shared.Transfers;

public interface ITransferCancellationRegistry
{
    CancellationToken Register(TransferKey key);

    void Unregister(TransferKey key);

    bool RequestCancellation(TransferKey key);
}
