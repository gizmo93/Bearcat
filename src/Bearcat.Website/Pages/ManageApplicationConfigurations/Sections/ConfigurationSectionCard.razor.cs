using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Sections;

public partial class ConfigurationSectionCard : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ConfigurationPageSection Section { get; set; } = null!;

    [Parameter]
    public EventCallback<ConfigurationSettingSaveRequest> OnSave { get; set; }

    [Parameter]
    public EventCallback<ApplicationConfigurationPropertyDto> OnReset { get; set; }

    private string TitleId => $"{Section.AnchorId}-title";

    private bool IsNotificationSection =>
        Section.Configuration.Key == ConfigurationSectionCatalog.NotificationsKey;

    private string? ContentClass => IsNotificationSection ? null : "pb-2";
}
