namespace Bearcat.Abstractions.SeriesDatabase;

public class SeriesDatabaseRateLimitExceededException(string databaseName, DateTimeOffset? resetAt)
    : Exception($"Rate limit exceeded for series database {databaseName}.")
{
    public string DatabaseName { get; } = databaseName;

    public DateTimeOffset? ResetAt { get; } = resetAt;
}
