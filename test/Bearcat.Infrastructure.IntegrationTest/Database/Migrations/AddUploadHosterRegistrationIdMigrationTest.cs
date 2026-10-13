using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Migrations;

public class AddUploadHosterRegistrationIdMigrationTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string PreviousMigrationName = "AddUploadedFilesHosterFileLinkIndex";
    private const string MigrationName = "AddUploadHosterRegistrationId";

    [TearDown]
    public async Task MigrateToLatestMigrationAsync()
    {
        await DbContext.Database.MigrateAsync();
    }

    [Test]
    public async Task Up_ExistingUploads_BackfillsHosterRegistrationIdFromUploadConfig()
    {
        // Arrange
        var testData = new MaterializedValuesTestData(DbContext);
        var alphaHosterRegistration = testData.AddHosterRegistration("Alpha");
        var betaHosterRegistration = testData.AddHosterRegistration("Beta");
        var release = testData.AddRelease("Release");
        var alphaUploadConfig = testData.AddUploadConfig(release, alphaHosterRegistration);
        var betaUploadConfig = testData.AddUploadConfig(release, betaHosterRegistration);
        var firstAlphaUpload = testData.AddUpload(alphaUploadConfig, OnlineState.Online);
        var secondAlphaUpload = testData.AddUpload(alphaUploadConfig, OnlineState.Offline);
        var betaUpload = testData.AddUpload(betaUploadConfig, OnlineState.Online);
        secondAlphaUpload.HosterRegistration = betaHosterRegistration;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        await MigrateAsync(PreviousMigrationName);

        // Act
        await MigrateAsync(MigrationName);

        // Assert
        var hosterRegistrationIdsByUploadId = await DbContext
            .Uploads.Select(upload => new { upload.Id, upload.HosterRegistrationId })
            .ToDictionaryAsync(upload => upload.Id, upload => upload.HosterRegistrationId);
        hosterRegistrationIdsByUploadId.ShouldBe(
            new Dictionary<int, int?>
            {
                [firstAlphaUpload.Id] = alphaHosterRegistration.Id,
                [secondAlphaUpload.Id] = alphaHosterRegistration.Id,
                [betaUpload.Id] = betaHosterRegistration.Id,
            },
            ignoreOrder: true
        );
    }

    private async Task MigrateAsync(string targetMigration)
    {
        await DbContext.GetService<IMigrator>().MigrateAsync(targetMigration);
        DbContext.ChangeTracker.Clear();
    }
}
