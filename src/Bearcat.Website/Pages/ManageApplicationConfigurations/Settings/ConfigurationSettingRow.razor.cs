using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;

public partial class ConfigurationSettingRow : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ApplicationConfigurationPropertyDto Setting { get; set; } = null!;

    [Parameter]
    public EventCallback<ConfigurationSettingSaveRequest> OnSave { get; set; }

    [Parameter]
    public EventCallback<ApplicationConfigurationPropertyDto> OnReset { get; set; }

    private string RowClass =>
        ConfigurationSettingFormatter.IsSwitch(Setting)
            ? "grid grid-cols-[minmax(0,1fr)_auto] items-start gap-x-8 gap-y-3 py-4"
            : "grid grid-cols-[minmax(0,1fr)] gap-x-8 gap-y-3 py-4 @xl:grid-cols-[minmax(0,1fr)_auto] @xl:items-center";

    private string DefaultValueText =>
        ConfigurationSettingFormatter.FormatValue(L, Setting, Setting.DefaultValue);

    private IReadOnlyList<SelectOption<string>> SelectOptions =>
        Setting
            .Options.Select(option => new SelectOption<string>(
                option,
                ConfigurationSettingFormatter.FormatOptionLabel(L, Setting, option)
            ))
            .ToList();

    private async Task SaveSwitchAsync(bool value)
    {
        await OnSave.InvokeAsync(new ConfigurationSettingSaveRequest(Setting, value));
    }

    private async Task SaveOptionAsync(string? value)
    {
        if (value == ConfigurationSettingFormatter.FormatOptionValue(Setting.CurrentValue))
        {
            return;
        }

        await OnSave.InvokeAsync(
            new ConfigurationSettingSaveRequest(
                Setting,
                ConfigurationSettingFormatter.ParseOptionValue(Setting, value)
            )
        );
    }

    private async Task ResetAsync()
    {
        await OnReset.InvokeAsync(Setting);
    }
}
