using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Website.Pages.ManageApplicationConfigurations.Notifications;
using Bearcat.Website.Pages.ManageApplicationConfigurations.Sections;
using Bearcat.Website.Pages.ManageApplicationConfigurations.Settings;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations;

public partial class ApplicationConfigurationsPage(
    IScopedOperationRunner operationRunner,
    ToastService toastService,
    NavigationManager navigationManager,
    IJSRuntime jsRuntime
) : ComponentBase, IAsyncDisposable
{
    private IReadOnlyList<ApplicationConfigurationDto> configurations = [];
    private bool isLoading = true;
    private string? searchTerm;
    private ConfigurationSettingFilter settingFilter = ConfigurationSettingFilter.All;
    private ElementReference pageElement;
    private IJSObjectReference? sectionSpyHandle;
    private string? attachedSectionSpyAnchorIds;
    private bool isFragmentScrollPending = true;

    private int TotalSettingCount =>
        configurations.Sum(configuration => configuration.Properties.Count);

    private int ChangedSettingCount =>
        configurations.Sum(configuration =>
            configuration.Properties.Count(setting => setting.IsOverridden)
        );

    private string EmptyStateText =>
        settingFilter == ConfigurationSettingFilter.Changed && ChangedSettingCount == 0
            ? L["NoChangedConfigurations"]
            : L["NoConfigurationsFound"];

    private IReadOnlyList<ConfigurationPageSection> VisibleSections =>
        configurations
            .OrderBy(configuration => ConfigurationSectionCatalog.GetPosition(configuration.Key))
            .Select(configuration => new ConfigurationPageSection(
                Configuration: configuration,
                Appearance: ConfigurationSectionCatalog.GetAppearance(configuration.Key),
                AnchorId: ConfigurationSectionCatalog.GetAnchorId(configuration.Key),
                VisibleSettings: configuration
                    .Properties.Where(setting => IsVisible(configuration, setting))
                    .ToList()
            ))
            .Where(section => section.VisibleSettings.Count > 0)
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        isLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (isLoading)
        {
            return;
        }

        await AttachSectionSpyWhenSectionsChangedAsync();
        await ScrollToRequestedSectionAsync();
    }

    private async Task LoadAsync()
    {
        configurations = await operationRunner.RunAsync(
            (ApplicationConfigurationService service) => service.GetAllAsync(CancellationToken.None)
        );
    }

    private bool IsVisible(
        ApplicationConfigurationDto configuration,
        ApplicationConfigurationPropertyDto setting
    )
    {
        return (settingFilter == ConfigurationSettingFilter.All || setting.IsOverridden)
            && MatchesSearchTerm(configuration, setting);
    }

    private bool MatchesSearchTerm(
        ApplicationConfigurationDto configuration,
        ApplicationConfigurationPropertyDto setting
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

        var searchableText = string.Join(' ', GetSearchableTexts(configuration, setting));

        return searchWords.All(word =>
            searchableText.Contains(word, StringComparison.OrdinalIgnoreCase)
        );
    }

    private List<string> GetSearchableTexts(
        ApplicationConfigurationDto configuration,
        ApplicationConfigurationPropertyDto setting
    )
    {
        List<string> searchableTexts =
        [
            configuration.DisplayName,
            L[configuration.DisplayName],
            setting.Name,
            L[setting.DisplayName],
        ];

        if (!string.IsNullOrWhiteSpace(configuration.Description))
        {
            searchableTexts.Add(L[configuration.Description]);
        }

        if (!string.IsNullOrWhiteSpace(setting.Description))
        {
            searchableTexts.Add(L[setting.Description]);
        }

        if (configuration.Key == ConfigurationSectionCatalog.NotificationsKey)
        {
            searchableTexts.Add(
                L[$"NotificationGroup.{NotificationSettingGroupLookup.GetGroup(setting)}"]
            );
        }

        return searchableTexts;
    }

    private async Task SaveSettingAsync(ConfigurationSettingSaveRequest request)
    {
        await operationRunner.RunAsync(
            (ApplicationConfigurationService service) =>
                service.SaveOverrideAsync(
                    configurationKey: request.Setting.ConfigurationKey,
                    propertyName: request.Setting.Name,
                    value: request.Value,
                    cancellationToken: CancellationToken.None
                )
        );
        toastService.Success(L["ConfigurationSettingSaved", L[request.Setting.DisplayName]]);
        await LoadAsync();
    }

    private async Task ResetSettingAsync(ApplicationConfigurationPropertyDto setting)
    {
        await operationRunner.RunAsync(
            (ApplicationConfigurationService service) =>
                service.ResetOverrideAsync(
                    configurationKey: setting.ConfigurationKey,
                    propertyName: setting.Name,
                    cancellationToken: CancellationToken.None
                )
        );
        toastService.Success(L["ConfigurationSettingReset", L[setting.DisplayName]]);
        await LoadAsync();
    }

    private async Task AttachSectionSpyWhenSectionsChangedAsync()
    {
        var anchorIds = VisibleSections.Select(section => section.AnchorId).ToList();
        var joinedAnchorIds = string.Join(',', anchorIds);

        if (joinedAnchorIds == attachedSectionSpyAnchorIds)
        {
            return;
        }

        attachedSectionSpyAnchorIds = joinedAnchorIds;
        await DetachSectionSpyAsync();

        try
        {
            sectionSpyHandle = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "bearcat.configurationSectionSpy.attach",
                pageElement,
                anchorIds
            );
        }
        catch (JSDisconnectedException) { }
    }

    private async Task ScrollToRequestedSectionAsync()
    {
        if (!isFragmentScrollPending)
        {
            return;
        }

        isFragmentScrollPending = false;
        var anchorId = new Uri(navigationManager.Uri).Fragment.TrimStart('#');

        if (anchorId.Length == 0)
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("bearcat.scrollElementIntoViewById", anchorId);
        }
        catch (JSDisconnectedException) { }
    }

    private async Task DetachSectionSpyAsync()
    {
        if (sectionSpyHandle is null)
        {
            return;
        }

        var handle = sectionSpyHandle;
        sectionSpyHandle = null;

        try
        {
            await handle.InvokeVoidAsync("detach");
            await handle.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await DetachSectionSpyAsync();
    }
}
