namespace Bearcat.NfoDatabases.Predb;

public class PredbDownloadQuota
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(1);

    private readonly Lock stateLock = new();
    private DateTimeOffset exhaustedUntil;

    public bool IsExhausted()
    {
        lock (stateLock)
        {
            return exhaustedUntil > DateTimeOffset.UtcNow;
        }
    }

    public DateTimeOffset MarkExhausted()
    {
        lock (stateLock)
        {
            exhaustedUntil = DateTimeOffset.UtcNow + Cooldown;
            return exhaustedUntil;
        }
    }
}
