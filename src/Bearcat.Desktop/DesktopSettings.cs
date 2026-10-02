using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using Npgsql;

namespace Bearcat.Desktop;

public sealed class DesktopSettings
{
    public static string DefaultSqliteFilePath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bearcat",
            "bearcat.db"
        );

    public List<string> WorkingDirectories { get; set; } = [];

    [JsonInclude]
    public string? ReleaseDataDirectory
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && WorkingDirectories.Count == 0)
            {
                WorkingDirectories.Add(value.Trim());
            }
        }
    }

    public string RarPath { get; set; } = "rar";

    public string SevenZipPath { get; set; } = "7z";

    public string BearcatHostPath { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<DatabaseProvider>))]
    public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.Postgres;

    public string SqliteFilePath { get; set; } = DefaultSqliteFilePath;

    public string PostgresHost { get; set; } = "localhost";

    public int PostgresPort { get; set; } = 5432;

    public string PostgresDatabase { get; set; } = "bearcat";

    public string PostgresUsername { get; set; } = "bearcat";

    public string PostgresPassword { get; set; } = "bearcat123";

    public int WebPort { get; set; } = 17208;

    public string ApiKey { get; set; } = string.Empty;

    public string WebUrl => $"http://127.0.0.1:{WebPort}";

    public static DesktopSettings CreateForNewInstallation()
    {
        return new DesktopSettings { DatabaseProvider = DatabaseProvider.Sqlite };
    }

    public string CreatePostgresConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = PostgresHost,
            Port = PostgresPort,
            Database = PostgresDatabase,
            Username = PostgresUsername,
            Password = PostgresPassword,
        };

        return builder.ConnectionString;
    }
}
