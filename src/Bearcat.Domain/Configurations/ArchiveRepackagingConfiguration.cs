using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration(
    "ArchiveRepackaging",
    "ArchiveRepackaging",
    "ArchiveRepackagingDescription"
)]
public class ArchiveRepackagingConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty(
        "ArchiveRepackagingStrategy",
        "ArchiveRepackagingStrategyDescription"
    )]
    [ApplicationConfigurationOptions(
        ArchiveRepackagingStrategies.NonceOnly,
        ArchiveRepackagingStrategies.SolidCompression,
        ArchiveRepackagingStrategies.IncrementArchiveFileSize
    )]
    public string Strategy { get; set; } = ArchiveRepackagingStrategies.IncrementArchiveFileSize;
}

public static class ArchiveRepackagingStrategies
{
    public const string NonceOnly = "NonceOnly";
    public const string SolidCompression = "SolidCompression";
    public const string IncrementArchiveFileSize = "IncrementArchiveFileSize";
}
