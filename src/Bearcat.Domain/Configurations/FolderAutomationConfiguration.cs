using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("FolderAutomation", "FolderAutomation", "FolderAutomationDescription")]
public class FolderAutomationConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty(
        "FolderAutomationStabilityMinutes",
        "FolderAutomationStabilityMinutesDescription"
    )]
    [ApplicationConfigurationUnit(ApplicationConfigurationUnit.Minutes)]
    public int StabilityMinutes { get; set; } = 5;

    [ApplicationConfigurationProperty(
        "FolderAutomationMinimumFolderSizeMegabytes",
        "FolderAutomationMinimumFolderSizeMegabytesDescription"
    )]
    [ApplicationConfigurationUnit(ApplicationConfigurationUnit.Megabytes)]
    public int MinimumFolderSizeMegabytes { get; set; } = 1;
}
