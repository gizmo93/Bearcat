namespace Bearcat.Domain.Shared.Transfers;

public sealed class TransferCancellationRegistry : ITransferCancellationRegistry
{
    private readonly Dictionary<TransferIdentifier, CancellationTokenSource> tokenSources = new();

    private readonly Lock gate = new();

    public CancellationToken Register(TransferIdentifier identifier)
    {
        var tokenSource = new CancellationTokenSource();

        lock (gate)
        {
            tokenSources[identifier] = tokenSource;
        }

        return tokenSource.Token;
    }

    public void Unregister(TransferIdentifier identifier)
    {
        lock (gate)
        {
            if (tokenSources.Remove(identifier, out var tokenSource))
            {
                tokenSource.Dispose();
            }
        }
    }

    public bool RequestCancellation(TransferIdentifier identifier)
    {
        lock (gate)
        {
            if (!tokenSources.TryGetValue(identifier, out var tokenSource))
            {
                return false;
            }

            tokenSource.Cancel();

            return true;
        }
    }
}
