using Bearcat.Infrastructure.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Bearcat.Infrastructure.Database;

public static class DatabaseConfiguration
{
    private const string ProviderKey = "Database:Provider";
    private const string PostgresConnectionStringKey = "Database:ConnectionString";
    private const string SqliteFilePathKey = "Database:SqliteFilePath";
    private const string DefaultSqliteFileName = "bearcat.db";

    public static DatabaseProvider GetProvider(IConfiguration configuration)
    {
        var configuredProvider = configuration[ProviderKey];
        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            return DatabaseProvider.Postgres;
        }

        if (
            Enum.TryParse<DatabaseProvider>(configuredProvider, ignoreCase: true, out var provider)
            && Enum.IsDefined(provider)
        )
        {
            return provider;
        }

        throw new InvalidOperationException(
            $"The database provider '{configuredProvider}' configured in '{ProviderKey}' is not supported. "
                + $"Supported values: {string.Join(", ", Enum.GetNames<DatabaseProvider>())}."
        );
    }

    public static string? GetPostgresConnectionString(IConfiguration configuration)
    {
        return configuration.GetRequiredSection(PostgresConnectionStringKey).Value;
    }

    public static string GetSqliteFilePath(IConfiguration configuration)
    {
        var configuredFilePath = configuration[SqliteFilePathKey];
        return string.IsNullOrWhiteSpace(configuredFilePath)
            ? Path.Combine(BearcatDataDirectory.Resolve(configuration), DefaultSqliteFileName)
            : Path.GetFullPath(configuredFilePath);
    }

    public static string CreateSqliteConnectionString(string sqliteFilePath)
    {
        return new SqliteConnectionStringBuilder { DataSource = sqliteFilePath }.ToString();
    }
}
