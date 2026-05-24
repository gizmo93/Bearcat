using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bearcat.Infrastructure.Security;

public sealed class RegistrationSecretMigration(
    Database.BearcatDbContext dbContext,
    ISecretProtector secretProtector,
    ILogger<RegistrationSecretMigration> logger
)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var migratedHosters = await MigrateTableAsync(
            tableName: "HosterRegistrations",
            cancellationToken: cancellationToken
        );
        var migratedLinkCrypters = await MigrateTableAsync(
            tableName: "LinkCrypterRegistrations",
            cancellationToken: cancellationToken
        );
        var migratedNfoDatabases = await MigrateTableAsync(
            tableName: "NfoDatabaseRegistrations",
            cancellationToken: cancellationToken
        );

        var migratedTotal = migratedHosters + migratedLinkCrypters + migratedNfoDatabases;
        if (migratedTotal == 0)
        {
            return;
        }

        logger.LogInformation(
            "Encrypted {MigratedSecretCount} registration configuration(s): {HosterCount} hoster, {LinkCrypterCount} link crypter, {NfoDatabaseCount} NFO database.",
            migratedTotal,
            migratedHosters,
            migratedLinkCrypters,
            migratedNfoDatabases
        );
    }

    private async Task<int> MigrateTableAsync(
        string tableName,
        CancellationToken cancellationToken = default
    )
    {
        var rows = await dbContext
            .Database.SqlQueryRaw<RegistrationSecretRow>(GetSelectSql(tableName))
            .ToListAsync(cancellationToken);

        var migratedCount = 0;
        foreach (var row in rows)
        {
            if (secretProtector.IsProtected(row.SerializedConfig))
            {
                continue;
            }

            var encryptedValue = secretProtector.Protect(row.SerializedConfig);
            await dbContext.Database.ExecuteSqlRawAsync(
                GetUpdateSql(tableName),
                [encryptedValue, row.Id],
                cancellationToken
            );

            migratedCount++;
        }

        return migratedCount;
    }

    private static string GetSelectSql(string tableName)
    {
        return tableName switch
        {
            "HosterRegistrations" => """
                SELECT "Id", "SerializedConfig"
                FROM "HosterRegistrations"
                """,
            "LinkCrypterRegistrations" => """
                SELECT "Id", "SerializedConfig"
                FROM "LinkCrypterRegistrations"
                """,
            "NfoDatabaseRegistrations" => """
                SELECT "Id", "SerializedConfig"
                FROM "NfoDatabaseRegistrations"
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, null),
        };
    }

    private static string GetUpdateSql(string tableName)
    {
        return tableName switch
        {
            "HosterRegistrations" => """
                UPDATE "HosterRegistrations"
                SET "SerializedConfig" = @p0
                WHERE "Id" = @p1
                """,
            "LinkCrypterRegistrations" => """
                UPDATE "LinkCrypterRegistrations"
                SET "SerializedConfig" = @p0
                WHERE "Id" = @p1
                """,
            "NfoDatabaseRegistrations" => """
                UPDATE "NfoDatabaseRegistrations"
                SET "SerializedConfig" = @p0
                WHERE "Id" = @p1
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, null),
        };
    }

    private sealed class RegistrationSecretRow
    {
        public int Id { get; set; }

        public string SerializedConfig { get; set; } = null!;
    }
}
