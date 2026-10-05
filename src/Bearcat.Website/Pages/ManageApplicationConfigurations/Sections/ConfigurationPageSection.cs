using Bearcat.Domain.UseCases.ManageApplicationConfigurations;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Sections;

public sealed record ConfigurationPageSection(
    ApplicationConfigurationDto Configuration,
    ConfigurationSectionAppearance Appearance,
    string AnchorId,
    IReadOnlyList<ApplicationConfigurationPropertyDto> VisibleSettings
)
{
    public int ChangedSettingCount =>
        Configuration.Properties.Count(setting => setting.IsOverridden);
}
