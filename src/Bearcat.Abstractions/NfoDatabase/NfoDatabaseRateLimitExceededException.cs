namespace Bearcat.Abstractions.NfoDatabase;

public class NfoDatabaseRateLimitExceededException(string databaseName, DateTimeOffset? resetAt)
    : Exception(CreateMessage(databaseName, resetAt))
{
    public string DatabaseName { get; } = databaseName;

    public DateTimeOffset? ResetAt { get; } = resetAt;

    private static string CreateMessage(string databaseName, DateTimeOffset? resetAt)
    {
        return resetAt is null
            ? $"{databaseName} rate limit exceeded."
            : $"{databaseName} rate limit exceeded. Reset at {resetAt:O}.";
    }
}
