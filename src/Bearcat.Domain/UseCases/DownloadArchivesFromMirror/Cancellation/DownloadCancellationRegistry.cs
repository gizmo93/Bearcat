namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Cancellation;

public sealed class DownloadCancellationRegistry : IDownloadCancellationRegistry
{
    private readonly Dictionary<int, CancellationTokenSource> tokenSources = new();

    private readonly Lock gate = new();

    public CancellationToken Register(int archiveId)
    {
        var tokenSource = new CancellationTokenSource();

        lock (gate)
        {
            tokenSources[archiveId] = tokenSource;
        }

        return tokenSource.Token;
    }

    public void Unregister(int archiveId)
    {
        lock (gate)
        {
            if (tokenSources.Remove(archiveId, out var tokenSource))
            {
                tokenSource.Dispose();
            }
        }
    }

    public bool RequestCancellation(int archiveId)
    {
        lock (gate)
        {
            if (!tokenSources.TryGetValue(archiveId, out var tokenSource))
            {
                return false;
            }

            tokenSource.Cancel();

            return true;
        }
    }
}
