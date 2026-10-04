namespace Bearcat.Website.Shared;

public sealed class RestartableDelay : IDisposable
{
    private CancellationTokenSource? currentDelayCancellation;

    public async Task<bool> WaitAsync(TimeSpan delay)
    {
        Cancel();
        var delayCancellation = new CancellationTokenSource();
        currentDelayCancellation = delayCancellation;

        try
        {
            await Task.Delay(delay, delayCancellation.Token);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public void Cancel()
    {
        currentDelayCancellation?.Cancel();
        currentDelayCancellation?.Dispose();
        currentDelayCancellation = null;
    }

    public void Dispose()
    {
        Cancel();
    }
}
