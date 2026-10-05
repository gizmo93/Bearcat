using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Notifications;

public static class NotificationSettingGroupLookup
{
    private static readonly Dictionary<string, NotificationGroup> groupsBySettingName =
        NotificationDefinitions.All.ToDictionary(
            definition => definition.Kind.ToString(),
            definition => definition.Group
        );

    public static NotificationGroup GetGroup(ApplicationConfigurationPropertyDto setting)
    {
        return groupsBySettingName[setting.Name];
    }
}
