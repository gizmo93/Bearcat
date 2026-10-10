using Bearcat.Domain.Entities;
using Bearcat.Infrastructure.Database;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Migrations;

public class ReplaceAutoDeleteArchivesWithArchiveRetentionActionMigrationTest(
    DatabaseProvider databaseProvider
) : BearcatIntegrationTest(databaseProvider)
{
    private const string PreviousMigrationName = "AddArchiveStorageFolders";
    private const string MigrationName = "ReplaceAutoDeleteArchivesWithArchiveRetentionAction";

    [Test]
    public async Task Up_AutoDeleteArchivesEnabled_BecomesDeleteRetentionAction()
    {
        // Arrange
        await MigrateAsync(PreviousMigrationName);
        await AddOverrideAsync("AutoDeleteArchives", "true");

        // Act
        await MigrateAsync(MigrationName);

        // Assert
        var result = await DbContext
            .ApplicationConfigurationOverrides.Where(o => o.ConfigurationKey == "ArchiveCleanup")
            .ToListAsync();
        var configurationOverride = result.ShouldHaveSingleItem();
        configurationOverride.PropertyName.ShouldBe("ArchiveRetentionAction");
        configurationOverride.SerializedValue.ShouldBe("2");
    }

    [Test]
    public async Task Up_AutoDeleteArchivesDisabled_RemovesOverride()
    {
        // Arrange
        await MigrateAsync(PreviousMigrationName);
        await AddOverrideAsync("AutoDeleteArchives", "false");
        await AddOverrideAsync("ArchiveRetentionDays", "7");

        // Act
        await MigrateAsync(MigrationName);

        // Assert
        var result = await DbContext
            .ApplicationConfigurationOverrides.Where(o => o.ConfigurationKey == "ArchiveCleanup")
            .ToListAsync();
        result.ShouldHaveSingleItem().PropertyName.ShouldBe("ArchiveRetentionDays");
    }

    private async Task MigrateAsync(string targetMigration)
    {
        await DbContext.GetService<IMigrator>().MigrateAsync(targetMigration);
        DbContext.ChangeTracker.Clear();
    }

    private async Task AddOverrideAsync(string propertyName, string serializedValue)
    {
        DbContext.ApplicationConfigurationOverrides.Add(
            new ApplicationConfigurationOverride
            {
                ConfigurationKey = "ArchiveCleanup",
                PropertyName = propertyName,
                SerializedValue = serializedValue,
                UpdatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            }
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }
}
