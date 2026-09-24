using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Primitives;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations;

public partial class ApplicationConfigurationsPage(IScopedOperationRunner operationRunner)
{
    private static readonly Dictionary<string, NotificationGroup> notificationGroupsByPropertyName =
        NotificationDefinitions.All.ToDictionary(
            definition => definition.Kind.ToString(),
            definition => definition.Group
        );

    private readonly Dictionary<string, string?> editorValues = [];
    private IReadOnlyList<ApplicationConfigurationDto> configurations = [];
    private bool isLoading = true;
    private string? searchTerm;

    private IReadOnlyList<ApplicationConfigurationDto> FilteredConfigurations =>
        configurations
            .Where(configuration =>
                configuration.Properties.Any(property => MatchesSearchTerm(configuration, property))
            )
            .ToList();

    private IReadOnlyList<
        IGrouping<NotificationGroup?, ApplicationConfigurationPropertyDto>
    > GetPropertyGroups(ApplicationConfigurationDto configuration)
    {
        return configuration
            .Properties.Where(property => MatchesSearchTerm(configuration, property))
            .GroupBy(property => GetNotificationGroup(configuration, property))
            .ToList();
    }

    private static NotificationGroup? GetNotificationGroup(
        ApplicationConfigurationDto configuration,
        ApplicationConfigurationPropertyDto property
    )
    {
        return configuration.DisplayName == "NotificationSettings"
            ? notificationGroupsByPropertyName[property.Name]
            : null;
    }

    private bool MatchesSearchTerm(
        ApplicationConfigurationDto configuration,
        ApplicationConfigurationPropertyDto property
    )
    {
        var searchWords = (searchTerm ?? string.Empty).Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
        );

        if (searchWords.Length == 0)
        {
            return true;
        }

        var searchableText = string.Join(' ', GetSearchableTexts(configuration, property));

        return searchWords.All(word =>
            searchableText.Contains(word, StringComparison.OrdinalIgnoreCase)
        );
    }

    private List<string> GetSearchableTexts(
        ApplicationConfigurationDto configuration,
        ApplicationConfigurationPropertyDto property
    )
    {
        List<string> searchableTexts =
        [
            configuration.DisplayName,
            L[configuration.DisplayName],
            property.Name,
            L[property.DisplayName],
        ];

        if (!string.IsNullOrWhiteSpace(configuration.Description))
        {
            searchableTexts.Add(L[configuration.Description]);
        }

        if (!string.IsNullOrWhiteSpace(property.Description))
        {
            searchableTexts.Add(L[property.Description]);
        }

        var notificationGroup = GetNotificationGroup(configuration, property);

        if (notificationGroup is not null)
        {
            searchableTexts.Add(L[$"NotificationGroup.{notificationGroup}"]);
        }

        return searchableTexts;
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        configurations = await operationRunner.RunAsync(
            (ApplicationConfigurationService service) => service.GetAllAsync(CancellationToken.None)
        );
        editorValues.Clear();
        isLoading = false;
    }

    private static bool GetBoolValue(ApplicationConfigurationPropertyDto property)
    {
        return property.CurrentValue is true;
    }

    private async Task SaveBoolAsync(ApplicationConfigurationPropertyDto property, bool value)
    {
        await operationRunner.RunAsync(
            (ApplicationConfigurationService service) =>
                service.SaveOverrideAsync(
                    configurationKey: property.ConfigurationKey,
                    propertyName: property.Name,
                    value: value,
                    cancellationToken: CancellationToken.None
                )
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

        await operationRunner.RunAsync(
            (ApplicationConfigurationService service) =>
                service.SaveOverrideAsync(
                    configurationKey: property.ConfigurationKey,
                    propertyName: property.Name,
                    value: value,
                    cancellationToken: CancellationToken.None
                )
        );
        await LoadAsync();
    }

    private async Task ResetOverrideAsync(ApplicationConfigurationPropertyDto property)
    {
        await operationRunner.RunAsync(
            (ApplicationConfigurationService service) =>
                service.ResetOverrideAsync(
                    configurationKey: property.ConfigurationKey,
                    propertyName: property.Name,
                    cancellationToken: CancellationToken.None
                )
        );
        await LoadAsync();
    }

    private string FormatValue(ApplicationConfigurationPropertyDto property, object? value)
    {
        if (property.ValueType == typeof(bool))
        {
            return value is true ? L["Enabled"] : L["Disabled"];
        }

        var stringValue = value?.ToString();

        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return "-";
        }

        return HasSelectOptions(property) ? FormatOptionValue(property, stringValue) : stringValue;
    }

    private static bool HasSelectOptions(ApplicationConfigurationPropertyDto property)
    {
        return property.ValueType == typeof(string) && property.Options.Count > 0;
    }

    private IReadOnlyList<SelectOption<string>> GetSelectOptions(
        ApplicationConfigurationPropertyDto property
    )
    {
        return property
            .Options.Select(value => new SelectOption<string>(
                value,
                FormatOptionValue(property, value)
            ))
            .ToList();
    }

    private string GetSelectClass(ApplicationConfigurationPropertyDto property)
    {
        var longestOptionLength = property
            .Options.Select(option => FormatOptionValue(property, option).Length)
            .DefaultIfEmpty(16)
            .Max();
        var width = Math.Clamp(longestOptionLength + 6, 16, 56) * 8;

        return $"max-w-full w-[min(100%,{width}px)]";
    }

    private string FormatOptionValue(ApplicationConfigurationPropertyDto? property, string value)
    {
        var localizedValue = property is null ? L[value] : L[$"{property.DisplayName}.{value}"];

        return localizedValue.ResourceNotFound ? value : localizedValue;
    }

    private static string GetEditorKey(ApplicationConfigurationPropertyDto property)
    {
        return $"{property.ConfigurationKey}.{property.Name}";
    }
}
