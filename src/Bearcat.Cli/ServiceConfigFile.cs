using System.Text.Json;

namespace Bearcat.Cli;

public sealed class ServiceConfigFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public DatabaseSection Database { get; init; } = new();
    public ArchiversSection Archivers { get; init; } = new();
    public string ReleaseDataDirectory { get; init; } = string.Empty;
    public string Urls { get; init; } = string.Empty;

    public static ServiceConfigFile Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ServiceConfigFile>(json, SerializerOptions)
            ?? new ServiceConfigFile();
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }

    public sealed class DatabaseSection
    {
        public string ConnectionString { get; set; } = string.Empty;
    }

    public sealed class ArchiversSection
    {
        public string RarPath { get; set; } = string.Empty;
        public string SevenZipPath { get; set; } = string.Empty;
    }
}
