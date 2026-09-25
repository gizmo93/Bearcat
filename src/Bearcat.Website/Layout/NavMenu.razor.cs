using Bearcat.Website.Layout.Navigation;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives.Sheet;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace Bearcat.Website.Layout;

public sealed partial class NavMenu(
    NavigationManager navigationManager,
    IJSRuntime js,
    PersistentComponentState applicationState,
    IHttpContextAccessor httpContextAccessor,
    NavMenuState navState
) : ComponentBase, IDisposable
{
    private PersistingComponentStateSubscription persistSubscription;

    [CascadingParameter]
    private SidebarContext? SidebarContext { get; set; }

    [CascadingParameter]
    private SheetContext? SheetContext { get; set; }

    protected override void OnInitialized()
    {
        navigationManager.LocationChanged += HandleLocationChanged;

        persistSubscription = applicationState.RegisterOnPersisting(PersistOpenStates);

        if (!navState.Initialized)
        {
            RestoreOpenStates();
            navState.Initialized = true;
        }

        OpenSubmenusContainingPath(navigationManager.Uri);
    }

    private void RestoreOpenStates()
    {
        foreach (var id in navState.OpenStateIds)
        {
            if (RestoreOpenState(OpenStateKey(id)) is { } open)
            {
                navState.SetOpen(id, open);
            }
        }
    }

    private bool? RestoreOpenState(string key)
    {
        if (applicationState.TryTakeFromJson<bool>(key, out var persisted))
        {
            return persisted;
        }

        return
            httpContextAccessor.HttpContext?.Request.Cookies.TryGetValue(key, out var cookie)
            == true
            ? cookie == "true"
            : null;
    }

    private Task PersistOpenStates()
    {
        foreach (var id in navState.OpenStateIds)
        {
            applicationState.PersistAsJson(OpenStateKey(id), navState.IsOpen(id));
        }

        return Task.CompletedTask;
    }

    private void OpenSubmenusContainingPath(string uri)
    {
        var baseRelativePath = navigationManager.ToBaseRelativePath(uri);

        foreach (
            var submenu in NavigationRegistry.Submenus.Where(submenu =>
                submenu.ContainsActiveEntryForPath(baseRelativePath)
            )
        )
        {
            navState.SetOpen(submenu.Id, true);
        }
    }

    private async Task SetOpenAsync(string id, bool open)
    {
        navState.SetOpen(id, open);
        await PersistCookieAsync(OpenStateKey(id), open);
    }

    private async Task ToggleSubmenuAsync(NavigationSubmenu submenu)
    {
        if (SidebarContext is { IsMobile: false, Open: false } collapsedSidebarContext)
        {
            navState.SetOpen(submenu.Id, true);
            collapsedSidebarContext.SetOpen(true);
        }
        else
        {
            navState.SetOpen(submenu.Id, !navState.IsOpen(submenu.Id));
        }

        await PersistCookieAsync(OpenStateKey(submenu.Id), navState.IsOpen(submenu.Id));
    }

    private async Task PersistCookieAsync(string key, bool open)
    {
        try
        {
            await js.InvokeVoidAsync("bearcat.setCookie", key, open ? "true" : "false");
        }
        catch (JSException) { }
    }

    private static string OpenStateKey(string id) => $"bearcat.nav.{id}Open";

    private static string ChevronClass(bool open) =>
        $"h-4 w-4 shrink-0 transition-transform {(open ? "rotate-180" : string.Empty)}";

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        OpenSubmenusContainingPath(args.Location);
        CloseMobileSidebar();
    }

    private void CloseMobileSidebar()
    {
        SheetContext?.Close();
        SidebarContext?.SetOpenMobile(false);
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        navigationManager.LocationChanged -= HandleLocationChanged;
        persistSubscription.Dispose();
    }
}
