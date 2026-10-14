using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Migrations;

public class AddReleaseOnlineStateAndUploadOldestUploadedFileCheckedAtMigrationTest(
    DatabaseProvider databaseProvider
) : BearcatIntegrationTest(databaseProvider)
{
    private const string PreviousMigrationName = "LimitUploadsUploadedAtIndexToUploadedUploads";
    private const string MigrationName =
        "AddReleaseOnlineStateAndUploadOldestUploadedFileCheckedAt";

    private static readonly DateTime LocalNow = MaterializedValuesTestData.LocalNow;

    [TearDown]
    public async Task MigrateToLatestMigrationAsync()
    {
        await DbContext.Database.MigrateAsync();
    }

    [Test]
    public async Task Up_ExistingReleasesAndUploads_BackfillsOnlineStateAndOldestUploadedFileCheckedAt()
    {
        // Arrange
        var testData = new MaterializedValuesTestData(DbContext);
        var hosterRegistration = testData.AddHosterRegistration("Alpha");
        var releaseWithoutUploadConfigs = testData.AddRelease("Unknown");
        var offlineRelease = testData.AddRelease("Offline");
        testData.AddUpload(
            testData.AddUploadConfig(offlineRelease, hosterRegistration),
            OnlineState.Offline
        );
        var partiallyOnlineRelease = testData.AddRelease("PartiallyOnline");
        testData.AddUpload(
            testData.AddUploadConfig(partiallyOnlineRelease, hosterRegistration),
            OnlineState.Online
        );
        testData.AddUploadConfig(partiallyOnlineRelease, hosterRegistration);
        var onlineRelease = testData.AddRelease("Online");
        var onlineUploadConfig = testData.AddUploadConfig(onlineRelease, hosterRegistration);
        var uploadWithoutFiles = testData.AddUpload(onlineUploadConfig, OnlineState.Offline);
        var uploadWithNeverCheckedFile = testData.AddUpload(
            onlineUploadConfig,
            OnlineState.Online,
            LocalNow.AddHours(-1),
            null
        );
        var uploadWithCheckedFiles = testData.AddUpload(
            onlineUploadConfig,
            OnlineState.Online,
            LocalNow.AddHours(-1),
            LocalNow.AddHours(-4)
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        await MigrateAsync(PreviousMigrationName);

        // Act
        await MigrateAsync(MigrationName);

        // Assert
        var onlineStatesByReleaseId = await DbContext.Releases.ToDictionaryAsync(
            release => release.Id,
            release => release.OnlineState
        );
        onlineStatesByReleaseId.ShouldBe(
            new Dictionary<int, OnlineState>
            {
                [releaseWithoutUploadConfigs.Id] = OnlineState.Unknown,
                [offlineRelease.Id] = OnlineState.Offline,
                [partiallyOnlineRelease.Id] = OnlineState.PartiallyOnline,
                [onlineRelease.Id] = OnlineState.Online,
            },
            ignoreOrder: true
        );
        var oldestCheckedAtsByUploadId = await DbContext
            .Uploads.Select(upload => new { upload.Id, upload.OldestUploadedFileCheckedAt })
            .ToDictionaryAsync(upload => upload.Id, upload => upload.OldestUploadedFileCheckedAt);
        oldestCheckedAtsByUploadId[uploadWithoutFiles.Id].ShouldBeNull();
        oldestCheckedAtsByUploadId[uploadWithNeverCheckedFile.Id].ShouldBe(DateTime.MinValue);
        oldestCheckedAtsByUploadId[uploadWithCheckedFiles.Id].ShouldBe(LocalNow.AddHours(-4));
    }

    private async Task MigrateAsync(string targetMigration)
    {
        await DbContext.GetService<IMigrator>().MigrateAsync(targetMigration);
        DbContext.ChangeTracker.Clear();
    }
}
