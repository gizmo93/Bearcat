using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("ArchiveCreation", "ArchiveCreation", "ArchiveCreationDescription")]
public class ArchiveCreationConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty("ArchivePartSizeUnit", "ArchivePartSizeUnitDescription")]
    public ArchivePartSizeUnit PartSizeUnit { get; set; } = ArchivePartSizeUnit.Megabytes;

    [ApplicationConfigurationProperty(
        "ArchivePackingExcludePatterns",
        "ArchivePackingExcludePatternsDescription"
    )]
    public string PackingExcludePatterns { get; set; } = "@eaDir/";
}
