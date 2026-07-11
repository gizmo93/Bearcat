namespace Bearcat.Abstractions.MediaMetadataDatabase;

public class MediaMetadataDatabaseRateLimitExceededException(
    string databaseName,
    DateTimeOffset? resetAt
) : Exception($"Rate limit reached for {databaseName}.")
{
    public DateTimeOffset? ResetAt { get; } = resetAt;
}
