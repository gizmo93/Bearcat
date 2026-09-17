using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("ArchiveCleanup", "ArchiveCleanup", "ArchiveCleanupDescription")]
public class ArchiveCleanupConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty(
        "AutoConvertToUnmanaged",
        "AutoConvertToUnmanagedDescription"
    )]
    public bool AutoConvertToUnmanaged { get; set; } = false;

    [ApplicationConfigurationProperty(
        "ReleaseFolderRetentionDays",
        "ReleaseFolderRetentionDaysDescription"
    )]
    public int ReleaseFolderRetentionDays { get; set; } = 14;

    [ApplicationConfigurationProperty("AutoDeleteArchives", "AutoDeleteArchivesDescription")]
    public bool AutoDeleteArchives { get; set; } = false;

    [ApplicationConfigurationProperty("ArchiveRetentionDays", "ArchiveRetentionDaysDescription")]
    public int ArchiveRetentionDays { get; set; } = 30;
}
