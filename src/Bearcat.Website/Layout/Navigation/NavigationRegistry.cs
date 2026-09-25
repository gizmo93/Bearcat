using Microsoft.AspNetCore.Components.Routing;

namespace Bearcat.Website.Layout.Navigation;

public static class NavigationRegistry
{
    public static IReadOnlyList<NavigationSection> Sections { get; } =
    [
        new NavigationSection(
            "main",
            "Navigation",
            NavigationSectionHeadingKind.HiddenInSidebar,
            [
                Entry("Home", "/", "house", NavLinkMatch.All),
                Entry("Dashboard", "/dashboard", "chart-column-stacked"),
                Entry("Releases", "/releases", "upload"),
                Entry("ReleaseCollections", "/release-collections", "boxes"),
                new NavigationSubmenu(
                    "activity",
                    "Activity",
                    "radar",
                    [
                        Entry("BackgroundTasks", "/background-tasks", "activity"),
                        Entry("RemoteSourceDownloads", "/remote-source-downloads", "history"),
                        Entry("Logs", "/logs", "scroll-text"),
                    ]
                ),
            ]
        ),
        new NavigationSection(
            "configuration",
            "Configuration",
            NavigationSectionHeadingKind.Plain,
            [
                new NavigationSubmenu(
                    "releaseSettings",
                    "ReleaseSettings",
                    "sliders-horizontal",
                    [
                        Entry("ReleaseTemplates", "/release-templates", "copy"),
                        Entry("ReleaseGroups", "/release-groups", "layers"),
                        Entry("QualityProfiles", "/quality-profiles", "shield-check"),
                        Entry("ForumPostTemplates", "/forum-post-templates", "message-square-text"),
                    ]
                ),
                new NavigationSubmenu(
                    "automation",
                    "Automation",
                    "workflow",
                    [
                        Entry(
                            "ReleaseFolderAutomations",
                            "/release-folder-automations",
                            "folder-cog"
                        ),
                        Entry(
                            "RemoteSourceAutomations",
                            "/remote-source-automations",
                            "folder-down"
                        ),
                    ]
                ),
                new NavigationSubmenu(
                    "system",
                    "System",
                    "monitor-cog",
                    [
                        Entry("Configurations", "/configurations", "settings"),
                        Entry("TelegramNotifications", "/telegram", "send"),
                    ]
                ),
            ]
        ),
        new NavigationSection(
            "registrations",
            "Registrations",
            NavigationSectionHeadingKind.Collapsible,
            [
                EntryWithTooltip(
                    "HosterRegistrations",
                    "Hosters",
                    "/hoster-registrations",
                    "key-round"
                ),
                Entry("RemoteSources", "/remote-sources", "server"),
                EntryWithTooltip(
                    "CrypterRegistrations",
                    "Crypters",
                    "/link-crypter-registrations",
                    "shield"
                ),
                EntryWithTooltip(
                    "ImageHosterRegistrations",
                    "ImageHosters",
                    "/image-hoster-registrations",
                    "image-up"
                ),
                EntryWithTooltip(
                    "DistributionSiteRegistrations",
                    "DistributionSites",
                    "/distribution-site-registrations",
                    "send"
                ),
                EntryWithTooltip(
                    "NfoDatabaseRegistrations",
                    "NfoDatabases",
                    "/nfo-database-registrations",
                    "database"
                ),
                EntryWithTooltip(
                    "MediaDatabaseRegistrations",
                    "MediaDatabases",
                    "/media-database-registrations",
                    "tv"
                ),
            ]
        ),
    ];

    public static IReadOnlyList<NavigationSubmenu> Submenus { get; } =
        Sections.SelectMany(section => section.Items).OfType<NavigationSubmenu>().ToList();

    public static IReadOnlyList<NavigationSection> CollapsibleSections { get; } =
        Sections
            .Where(section => section.HeadingKind == NavigationSectionHeadingKind.Collapsible)
            .ToList();

    private static NavigationEntry Entry(
        string labelKey,
        string href,
        string iconName,
        NavLinkMatch match = NavLinkMatch.Prefix
    ) => new(labelKey, labelKey, href, match, iconName);

    private static NavigationEntry EntryWithTooltip(
        string labelKey,
        string tooltipKey,
        string href,
        string iconName
    ) => new(labelKey, tooltipKey, href, NavLinkMatch.Prefix, iconName);
}
