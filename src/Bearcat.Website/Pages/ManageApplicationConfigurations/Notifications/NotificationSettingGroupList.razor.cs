using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Notifications;

public partial class NotificationSettingGroupList : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ApplicationConfigurationPropertyDto> Settings { get; set; } = null!;

    [Parameter]
    public EventCallback<ConfigurationSettingSaveRequest> OnSave { get; set; }

    [Parameter]
    public EventCallback<ApplicationConfigurationPropertyDto> OnReset { get; set; }

    private IReadOnlyList<
        IGrouping<NotificationGroup, ApplicationConfigurationPropertyDto>
    > Groups => Settings.GroupBy(NotificationSettingGroupLookup.GetGroup).ToList();
}
