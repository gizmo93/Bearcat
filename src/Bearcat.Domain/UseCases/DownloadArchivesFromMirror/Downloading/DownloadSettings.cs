namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public sealed record DownloadSettings(
    int MaxAttempts,
    TimeSpan RetryDelay,
    int MaxParallelDownloads
);
