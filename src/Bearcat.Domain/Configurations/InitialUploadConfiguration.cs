using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration("InitialUpload", "InitialUpload", "InitialUploadDescription")]
public class InitialUploadConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty(
        "InitialUploadCooldownMinutes",
        "InitialUploadCooldownMinutesDescription"
    )]
    public int CooldownMinutes { get; set; } = 5;
}
