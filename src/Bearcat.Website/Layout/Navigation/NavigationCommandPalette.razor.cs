using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace Bearcat.Website.Layout.Navigation;

public sealed partial class NavigationCommandPalette(
    NavigationManager navigationManager,
    IKeyboardShortcutService keyboardShortcutService,
    PersistentComponentState applicationState,
    IHttpContextAccessor httpContextAccessor
) : ComponentBase, IDisposable
{
    private const string OpenPaletteShortcut = "Ctrl+K";
    private const string IsMacPlatformStateKey = "bearcat.navigationCommandPalette.isMacPlatform";

    private static readonly IReadOnlyList<CommandGroup> commandGroups = BuildCommandGroups();

    private PersistingComponentStateSubscription persistSubscription;
    private IDisposable? shortcutRegistration;
    private bool isOpen;
    private bool isMacPlatform;

    private string ShortcutHint => isMacPlatform ? "⌘K" : "Ctrl K";

    protected override void OnInitialized()
    {
        persistSubscription = applicationState.RegisterOnPersisting(PersistIsMacPlatform);

        isMacPlatform = applicationState.TryTakeFromJson<bool>(
            IsMacPlatformStateKey,
            out var persisted
        )
            ? persisted
            : IsMacUserAgent(httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString());
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        shortcutRegistration = await keyboardShortcutService.RegisterAsync(
            OpenPaletteShortcut,
            OpenPaletteFromShortcutAsync
        );
    }

    private Task PersistIsMacPlatform()
    {
        applicationState.PersistAsJson(IsMacPlatformStateKey, isMacPlatform);
        return Task.CompletedTask;
    }

    private static bool IsMacUserAgent(string? userAgent) =>
        userAgent?.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase) == true;

    private void OpenPalette()
    {
        isOpen = true;
    }

    private Task OpenPaletteFromShortcutAsync()
    {
        isOpen = true;
        return InvokeAsync(StateHasChanged);
    }

    private void NavigateToPage(string href)
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        navigationManager.NavigateTo(href);
    }

    private string BuildSearchText(NavigationEntry entry, CommandGroup group) =>
        string.Join(
            ' ',
            [L[entry.LabelKey].Value, .. group.SearchLabelKeys.Select(key => L[key].Value)]
        );

    private static List<CommandGroup> BuildCommandGroups()
    {
        var groups = new List<CommandGroup>();

        foreach (var section in NavigationRegistry.Sections)
        {
            var sectionEntries = section.Items.OfType<NavigationEntry>().ToList();
            if (sectionEntries.Count > 0)
            {
                groups.Add(
                    new CommandGroup(
                        section.HeadingLabelKey,
                        [section.HeadingLabelKey],
                        sectionEntries
                    )
                );
            }

            foreach (var submenu in section.Items.OfType<NavigationSubmenu>())
            {
                groups.Add(
                    new CommandGroup(
                        submenu.LabelKey,
                        [submenu.LabelKey, section.HeadingLabelKey],
                        submenu.Entries
                    )
                );
            }
        }

        return groups;
    }

    public void Dispose()
    {
        shortcutRegistration?.Dispose();
        persistSubscription.Dispose();
    }

    private sealed record CommandGroup(
        string HeadingLabelKey,
        IReadOnlyList<string> SearchLabelKeys,
        IReadOnlyList<NavigationEntry> Entries
    );
}
