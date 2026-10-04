using Bearcat.Website.Shared;
using BlazorBlueprint.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bearcat.Website.Layout.Navigation;

public sealed partial class NavigationCommandPalette(
    NavigationManager navigationManager,
    IKeyboardShortcutService keyboardShortcutService,
    ClientPlatform clientPlatform
) : ComponentBase, IDisposable
{
    private const string OpenPaletteShortcut = "Ctrl+K";

    private static readonly IReadOnlyList<CommandGroup> commandGroups = BuildCommandGroups();

    private IDisposable? shortcutRegistration;
    private bool isOpen;

    private string ShortcutHint => clientPlatform.IsMac ? "⌘K" : "Ctrl K";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            shortcutRegistration = await keyboardShortcutService.RegisterAsync(
                OpenPaletteShortcut,
                OpenPaletteFromShortcutAsync
            );
        }
        catch (Exception exception)
            when (exception is ObjectDisposedException or JSDisconnectedException)
        {
            return;
        }
    }

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
    }

    private sealed record CommandGroup(
        string HeadingLabelKey,
        IReadOnlyList<string> SearchLabelKeys,
        IReadOnlyList<NavigationEntry> Entries
    );
}
