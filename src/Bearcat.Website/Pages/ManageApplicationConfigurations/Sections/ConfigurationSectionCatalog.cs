using System.Text;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Sections;

public static class ConfigurationSectionCatalog
{
    public const string NotificationsKey = "Notifications";

    private static readonly List<string> orderedKeys =
    [
        "UploadConcurrency",
        "InitialUpload",
        "MirrorDownloads",
        "RemoteSourceDownloads",
        "FolderAutomation",
        "PostQueue",
        "ArchiveCleanup",
        "ArchiveRepackaging",
        NotificationsKey,
    ];

    private static readonly Dictionary<string, ConfigurationSectionAppearance> appearancesByKey =
        new()
        {
            ["UploadConcurrency"] = new("upload", ConfigurationCategory.Transfers),
            ["InitialUpload"] = new("timer", ConfigurationCategory.Transfers),
            ["MirrorDownloads"] = new("download", ConfigurationCategory.Transfers),
            ["RemoteSourceDownloads"] = new("server", ConfigurationCategory.Transfers),
            ["FolderAutomation"] = new("folder-sync", ConfigurationCategory.Automation),
            ["PostQueue"] = new("list-ordered", ConfigurationCategory.Automation),
            ["ArchiveCleanup"] = new("brush-cleaning", ConfigurationCategory.Archives),
            ["ArchiveRepackaging"] = new("package", ConfigurationCategory.Archives),
            [NotificationsKey] = new("bell", ConfigurationCategory.Notifications),
        };

    private static readonly Dictionary<string, int> positionsByKey = orderedKeys
        .Select((key, position) => (key, position))
        .ToDictionary(entry => entry.key, entry => entry.position);

    public static ConfigurationSectionAppearance GetAppearance(string configurationKey)
    {
        return appearancesByKey[configurationKey];
    }

    public static int GetPosition(string configurationKey)
    {
        return positionsByKey[configurationKey];
    }

    public static string GetAnchorId(string configurationKey)
    {
        var anchorId = new StringBuilder(configurationKey.Length + 4);

        foreach (var character in configurationKey)
        {
            if (char.IsUpper(character) && anchorId.Length > 0)
            {
                anchorId.Append('-');
            }

            anchorId.Append(char.ToLowerInvariant(character));
        }

        return anchorId.ToString();
    }
}
