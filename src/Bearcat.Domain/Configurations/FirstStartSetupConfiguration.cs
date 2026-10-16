using Bearcat.Abstractions.Configurations;

namespace Bearcat.Domain.Configurations;

[ApplicationConfiguration(
    "FirstStartSetup",
    "FirstStartSetup",
    "FirstStartSetupConfigurationDescription"
)]
public class FirstStartSetupConfiguration : IApplicationConfiguration
{
    [ApplicationConfigurationProperty(
        "ShowAssistantOnActivityPage",
        "ShowAssistantOnActivityPageDescription"
    )]
    public bool ShowAssistantOnActivityPage { get; set; } = true;
}
