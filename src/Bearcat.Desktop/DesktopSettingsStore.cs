using System;
using System.IO;
using System.Text.Json;

namespace Bearcat.Desktop;

public sealed class DesktopSettingsStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
    };

    public string SettingsPath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bearcat",
            "Desktop",
            "settings.json"
        );

    public DesktopSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new DesktopSettings();
        }

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<DesktopSettings>(json) ?? new DesktopSettings();
    }

    public void Save(DesktopSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(settings, JsonSerializerOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
