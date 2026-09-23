namespace Bearcat.Domain.Shared.Transfers;

public sealed class TransferCancellationRegistry : ITransferCancellationRegistry
{
    private readonly Dictionary<TransferKey, CancellationTokenSource> tokenSources = new();

    private readonly Lock gate = new();

    public CancellationToken Register(TransferKey key)
    {
        var tokenSource = new CancellationTokenSource();

        lock (gate)
        {
            tokenSources[key] = tokenSource;
        }

        return tokenSource.Token;
    }

    public void Unregister(TransferKey key)
    {
        lock (gate)
        {
            if (tokenSources.Remove(key, out var tokenSource))
            {
                tokenSource.Dispose();
            }
        }
    }

    public bool RequestCancellation(TransferKey key)
    {
        lock (gate)
        {
            if (!tokenSources.TryGetValue(key, out var tokenSource))
            {
                return false;
            }

            tokenSource.Cancel();

            return true;
        }
    }
}
