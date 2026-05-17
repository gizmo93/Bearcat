using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("ArchiveCleanup", "ArchiveCleanup", "ArchiveCleanupDescription")]
public class ArchiveCleanupConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty("AutoCleanup", "AutoCleanupDescription")]
    public bool AutoCleanup { get; set; } = false;
}
