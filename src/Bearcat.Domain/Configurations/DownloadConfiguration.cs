using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("MirrorDownloads", "MirrorDownloads", "MirrorDownloadsDescription")]
public class DownloadConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty("MaxParallelDownloads", "MaxParallelDownloadsDescription")]
    public int MaxParallelDownloads { get; set; } = 2;
}
