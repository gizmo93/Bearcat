using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Notifications;

public partial class NotificationSettingTile : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ApplicationConfigurationPropertyDto Setting { get; set; } = null!;

    [Parameter]
    public EventCallback<ConfigurationSettingSaveRequest> OnSave { get; set; }

    [Parameter]
    public EventCallback<ApplicationConfigurationPropertyDto> OnReset { get; set; }

    private async Task SaveAsync(bool value)
    {
        await OnSave.InvokeAsync(new ConfigurationSettingSaveRequest(Setting, value));
    }

    private async Task ResetAsync()
    {
        await OnReset.InvokeAsync(Setting);
    }
}
