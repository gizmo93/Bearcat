using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Bearcat.Cli;

public sealed class ServiceConfigFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public DatabaseSection Database { get; init; } = new();
    public ArchiversSection Archivers { get; init; } = new();
    public List<string> WorkingDirectories { get; init; } = [];

    [JsonInclude]
    public string? ReleaseDataDirectory
    {
        get => null;
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                WorkingDirectories.Add(value.Trim());
            }
        }
    }

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

        var configuration = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path))!.AsObject()
            : new JsonObject();
        configuration.Remove(nameof(ReleaseDataDirectory));
        if (configuration[nameof(Database)] is JsonObject existingDatabaseSection)
        {
            RemoveUnsetDatabaseKeys(existingDatabaseSection);
        }

        MergeInto(
            configuration,
            JsonSerializer.SerializeToNode(this, SerializerOptions)!.AsObject()
        );

        File.WriteAllText(path, configuration.ToJsonString(SerializerOptions));
    }

    private void RemoveUnsetDatabaseKeys(JsonObject existingDatabaseSection)
    {
        if (Database.Provider is null)
        {
            existingDatabaseSection.Remove(nameof(DatabaseSection.Provider));
        }

        if (Database.ConnectionString is null)
        {
            existingDatabaseSection.Remove(nameof(DatabaseSection.ConnectionString));
        }

        if (Database.SqliteFilePath is null)
        {
            existingDatabaseSection.Remove(nameof(DatabaseSection.SqliteFilePath));
        }
    }

    private static void MergeInto(JsonObject target, JsonObject ownedFields)
    {
        foreach (var (name, value) in ownedFields)
        {
            if (value is JsonObject ownedSection && target[name] is JsonObject existingSection)
            {
                MergeInto(existingSection, ownedSection);
            }
            else
            {
                target[name] = value?.DeepClone();
            }
        }
    }

    public sealed class DatabaseSection
    {
        [JsonConverter(typeof(JsonStringEnumConverter<DatabaseProvider>))]
        public DatabaseProvider? Provider { get; set; }

        public string? ConnectionString { get; set; }

        public string? SqliteFilePath { get; set; }

        [JsonIgnore]
        public DatabaseProvider EffectiveProvider => Provider ?? DatabaseProvider.Postgres;
    }

    public sealed class ArchiversSection
    {
        public string RarPath { get; set; } = string.Empty;
        public string SevenZipPath { get; set; } = string.Empty;
    }
}
