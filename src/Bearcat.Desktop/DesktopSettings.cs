using Npgsql;

namespace Bearcat.Desktop;

public sealed class DesktopSettings
{
    public string ReleaseDataDirectory { get; set; } = string.Empty;

    public string RarPath { get; set; } = "rar";

    public string SevenZipPath { get; set; } = "7z";

    public string BearcatHostPath { get; set; } = string.Empty;

    public string PostgresHost { get; set; } = "localhost";

    public int PostgresPort { get; set; } = 5432;

    public string PostgresDatabase { get; set; } = "bearcat";

    public string PostgresUsername { get; set; } = "bearcat";

    public string PostgresPassword { get; set; } = "bearcat123";

    public int WebPort { get; set; } = 17208;

    public string WebUrl => $"http://127.0.0.1:{WebPort}";

    public string CreateConnectionString()
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
