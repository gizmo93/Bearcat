using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations;

public partial class ApplicationConfigurationsPage
{
    private readonly Dictionary<string, string?> editorValues = [];
    private IReadOnlyList<ApplicationConfigurationDto> configurations = [];
    private ApplicationConfigurationService configurationService = null!;
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        configurationService = ScopedServices.GetRequiredService<ApplicationConfigurationService>();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        configurations = await configurationService.GetAllAsync(CancellationToken.None);
        editorValues.Clear();
        isLoading = false;
    }

    private static bool GetBoolValue(ApplicationConfigurationPropertyDto property)
    {
        return property.CurrentValue is true;
    }

    private async Task SaveBoolAsync(ApplicationConfigurationPropertyDto property, bool value)
    {
        await configurationService.SaveOverrideAsync(
            configurationKey: property.ConfigurationKey,
            propertyName: property.Name,
            value: value,
            cancellationToken: CancellationToken.None
        );
        await LoadAsync();
    }

    private string? GetEditorValue(ApplicationConfigurationPropertyDto property)
    {
        var key = GetEditorKey(property);

        if (editorValues.TryGetValue(key, out var value))
        {
            return value;
        }

        value = property.CurrentValue?.ToString();
        editorValues[key] = value;
        return value;
    }

    private void SetEditorValue(ApplicationConfigurationPropertyDto property, string? value)
    {
        editorValues[GetEditorKey(property)] = value;
    }

    private async Task SaveEditorValueAsync(ApplicationConfigurationPropertyDto property)
    {
        var editorValue = GetEditorValue(property);
        object? value = editorValue;

        if (property.ValueType == typeof(int) || property.ValueType == typeof(int?))
        {
            value = int.TryParse(editorValue, out var intValue) ? intValue : 0;
        }

        await configurationService.SaveOverrideAsync(
            configurationKey: property.ConfigurationKey,
            propertyName: property.Name,
            value: value,
            cancellationToken: CancellationToken.None
        );
        await LoadAsync();
    }

    private async Task ResetOverrideAsync(ApplicationConfigurationPropertyDto property)
    {
        await configurationService.ResetOverrideAsync(
            configurationKey: property.ConfigurationKey,
            propertyName: property.Name,
            cancellationToken: CancellationToken.None
        );
        await LoadAsync();
    }

    private string FormatValue(object? value, Type valueType)
    {
        if (valueType == typeof(bool))
        {
            return value is true ? L["Enabled"] : L["Disabled"];
        }

        return value?.ToString() ?? "-";
    }

    private static string GetEditorKey(ApplicationConfigurationPropertyDto property)
    {
        return $"{property.ConfigurationKey}.{property.Name}";
    }
}
