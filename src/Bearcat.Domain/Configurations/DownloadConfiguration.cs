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
    [ApplicationConfigurationUnit(ApplicationConfigurationUnit.Seconds)]
    public int DownloadRetryDelaySeconds { get; set; } = 30;

    [ApplicationConfigurationProperty(
        "DownloadSpeedLimitMegabytesPerSecond",
        "DownloadSpeedLimitMegabytesPerSecondDescription"
    )]
    [ApplicationConfigurationUnit(ApplicationConfigurationUnit.MegabytesPerSecond)]
    public decimal? DownloadSpeedLimitMegabytesPerSecond { get; set; }
}
