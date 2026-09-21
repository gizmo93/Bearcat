using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("MirrorDownloads", "MirrorDownloads", "MirrorDownloadsDescription")]
public class DownloadConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty("MaxParallelDownloads", "MaxParallelDownloadsDescription")]
    public int MaxParallelDownloads { get; set; } = 2;

    [ApplicationConfigurationProperty("MaxDownloadAttempts", "MaxDownloadAttemptsDescription")]
    public int MaxDownloadAttempts { get; set; } = 3;

    [ApplicationConfigurationProperty(
        "DownloadRetryDelaySeconds",
        "DownloadRetryDelaySecondsDescription"
    )]
    public int DownloadRetryDelaySeconds { get; set; } = 30;
}
