using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration(
    "RemoteSourceDownloads",
    "RemoteSourceDownloads",
    "RemoteSourceDownloadsDescription"
)]
public class RemoteSourceConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty(
        "RemoteSourceDownloadStabilityMinutes",
        "RemoteSourceDownloadStabilityMinutesDescription"
    )]
    [ApplicationConfigurationUnit(ApplicationConfigurationUnit.Minutes)]
    public int StabilityMinutes { get; set; } = 5;

    [ApplicationConfigurationProperty(
        "RemoteSourceDownloadMinimumFolderSizeMegabytes",
        "RemoteSourceDownloadMinimumFolderSizeMegabytesDescription"
    )]
    [ApplicationConfigurationUnit(ApplicationConfigurationUnit.Megabytes)]
    public int MinimumFolderSizeMegabytes { get; set; } = 1;

    [ApplicationConfigurationProperty(
        "RemoteSourceDownloadMaxParallelFileDownloads",
        "RemoteSourceDownloadMaxParallelFileDownloadsDescription"
    )]
    public int MaxParallelFileDownloads { get; set; } = 4;

    [ApplicationConfigurationProperty(
        "RemoteSourceDownloadMaxDownloadAttempts",
        "RemoteSourceDownloadMaxDownloadAttemptsDescription"
    )]
    public int MaxDownloadAttempts { get; set; } = 3;

    [ApplicationConfigurationProperty(
        "RemoteSourceDownloadRetryDelaySeconds",
        "RemoteSourceDownloadRetryDelaySecondsDescription"
    )]
    [ApplicationConfigurationUnit(ApplicationConfigurationUnit.Seconds)]
    public int DownloadRetryDelaySeconds { get; set; } = 30;
}
