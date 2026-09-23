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
    public int StabilityMinutes { get; set; } = 5;

    [ApplicationConfigurationProperty(
        "RemoteSourceDownloadMinimumFolderSizeMegabytes",
        "RemoteSourceDownloadMinimumFolderSizeMegabytesDescription"
    )]
    public int MinimumFolderSizeMegabytes { get; set; } = 1;
}
