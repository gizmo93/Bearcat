using System.Net.Http.Headers;
using Bearcat.Abstractions.NfoDatabase;

namespace Bearcat.NfoDatabases.Xrel;

public class XrelRateLimitState
{
    private readonly Lock stateLock = new();
    private int? remaining;
    private DateTimeOffset? resetAt;

    public void ThrowIfLimited()
    {
        lock (stateLock)
        {
            if (remaining <= 0 && resetAt > DateTimeOffset.UtcNow)
            {
                throw new NfoDatabaseRateLimitExceededException("xREL", resetAt);
            }
        }
    }

    public void Update(HttpResponseHeaders headers)
    {
        lock (stateLock)
        {
            remaining = ReadIntHeader(headers, "X-RateLimit-Remaining") ?? remaining;
            resetAt = ReadUnixTimeHeader(headers, "X-RateLimit-Reset") ?? resetAt;
        }
    }

    public DateTimeOffset? GetResetAt()
    {
        lock (stateLock)
        {
            return resetAt;
        }
    }

    private static int? ReadIntHeader(HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values))
        {
            return null;
        }

        return int.TryParse(values.FirstOrDefault(), out var value) ? value : null;
    }

    private static DateTimeOffset? ReadUnixTimeHeader(HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values))
        {
            return null;
        }

        return long.TryParse(values.FirstOrDefault(), out var value)
            ? DateTimeOffset.FromUnixTimeSeconds(value)
            : null;
    }
}
