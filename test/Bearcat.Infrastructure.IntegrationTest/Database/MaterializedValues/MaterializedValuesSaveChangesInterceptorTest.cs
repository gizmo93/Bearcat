using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;

public class MaterializedValuesSaveChangesInterceptorTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime LocalNow = MaterializedValuesTestData.LocalNow;

    private MaterializedValuesTestData testData = null!;

    [SetUp]
    public void Setup()
    {
        testData = new MaterializedValuesTestData(DbContext);
    }

    [Test]
    public async Task SaveChangesAsync_ReleaseWithoutUploadConfigs_StoresUnknown()
    {
        // Arrange
        var release = testData.AddRelease("Release");

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task SaveChangesAsync_UploadConfigWithoutUploads_StoresOffline()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        testData.AddUploadConfig(release, testData.AddHosterRegistration("Alpha"));

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Offline);
    }

    [Test]
    public async Task SaveChangesAsync_OnlineUploadAddedToOnlyUploadConfig_StoresOnline()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var uploadConfig = testData.AddUploadConfig(
            release,
            testData.AddHosterRegistration("Alpha")
        );
        await DbContext.SaveChangesAsync();
        testData.AddUpload(uploadConfig, OnlineState.Online);

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task SaveChangesAsync_OneOfTwoUploadConfigsHasOnlineUpload_StoresPartiallyOnline()
    {
        // Arrange
        var hosterRegistration = testData.AddHosterRegistration("Alpha");
        var release = testData.AddRelease("Release");
        testData.AddUpload(
            testData.AddUploadConfig(release, hosterRegistration),
            OnlineState.Online
        );
        testData.AddUpload(
            testData.AddUploadConfig(release, hosterRegistration),
            OnlineState.PartiallyOnline
        );

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.PartiallyOnline);
    }

    [Test]
    public async Task SaveChangesAsync_UploadConfigWithOfflineAndOnlineUpload_CountsUploadConfigAsOnline()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var uploadConfig = testData.AddUploadConfig(
            release,
            testData.AddHosterRegistration("Alpha")
        );
        testData.AddUpload(uploadConfig, OnlineState.Online);
        testData.AddUpload(uploadConfig, OnlineState.Offline);

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task SaveChangesAsync_UploadConfigOfInactiveHosterRegistrationWithoutOnlineUpload_CountsUploadConfigAsOffline()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        testData.AddUpload(
            testData.AddUploadConfig(release, testData.AddHosterRegistration("Active")),
            OnlineState.Online
        );
        testData.AddUploadConfig(
            release,
            testData.AddHosterRegistration("Inactive", isActive: false)
        );

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.PartiallyOnline);
    }

    [Test]
    public async Task SaveChangesAsync_UploadOnlineStateChanged_UpdatesReleaseOnlineState()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, testData.AddHosterRegistration("Alpha")),
            OnlineState.Online
        );
        await DbContext.SaveChangesAsync();
        upload.OnlineState = OnlineState.Offline;

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Offline);
    }

    [Test]
    public async Task SaveChangesAsync_UploadDeleted_UpdatesReleaseOnlineState()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, testData.AddHosterRegistration("Alpha")),
            OnlineState.Online
        );
        await DbContext.SaveChangesAsync();
        DbContext.Uploads.Remove(upload);

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Offline);
    }

    [Test]
    public async Task SaveChangesAsync_UploadConfigDeleted_UpdatesReleaseOnlineState()
    {
        // Arrange
        var hosterRegistration = testData.AddHosterRegistration("Alpha");
        var release = testData.AddRelease("Release");
        testData.AddUpload(
            testData.AddUploadConfig(release, hosterRegistration),
            OnlineState.Online
        );
        var offlineUploadConfig = testData.AddUploadConfig(release, hosterRegistration);
        await DbContext.SaveChangesAsync();
        DbContext.UploadConfigs.Remove(offlineUploadConfig);

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task SaveChangesAsync_UploadConfigMovedToOtherRelease_UpdatesBothReleases()
    {
        // Arrange
        var hosterRegistration = testData.AddHosterRegistration("Alpha");
        var sourceRelease = testData.AddRelease("Source");
        var targetRelease = testData.AddRelease("Target");
        var movedUploadConfig = testData.AddUploadConfig(sourceRelease, hosterRegistration);
        testData.AddUpload(movedUploadConfig, OnlineState.Online);
        testData.AddUploadConfig(sourceRelease, hosterRegistration);
        testData.AddUploadConfig(targetRelease, hosterRegistration);
        await DbContext.SaveChangesAsync();
        movedUploadConfig.Release = targetRelease;

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(sourceRelease)).ShouldBe(OnlineState.Offline);
        (await GetStoredOnlineStateAsync(targetRelease)).ShouldBe(OnlineState.PartiallyOnline);
    }

    [Test]
    public async Task SaveChangesAsync_UploadMovedToUploadConfigOfOtherRelease_UpdatesBothReleases()
    {
        // Arrange
        var hosterRegistration = testData.AddHosterRegistration("Alpha");
        var sourceRelease = testData.AddRelease("Source");
        var targetRelease = testData.AddRelease("Target");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(sourceRelease, hosterRegistration),
            OnlineState.Online
        );
        var targetUploadConfig = testData.AddUploadConfig(targetRelease, hosterRegistration);
        await DbContext.SaveChangesAsync();
        upload.UploadConfig = targetUploadConfig;

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(sourceRelease)).ShouldBe(OnlineState.Offline);
        (await GetStoredOnlineStateAsync(targetRelease)).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task SaveChangesAsync_UploadWithoutUploadedFiles_StoresNull()
    {
        // Arrange
        var upload = AddUploadOfNewRelease();

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBeNull();
    }

    [Test]
    public async Task SaveChangesAsync_UploadedFilesCheckedAtDifferentTimes_StoresOldestCheckedAt()
    {
        // Arrange
        var upload = AddUploadOfNewRelease(LocalNow.AddHours(-1), LocalNow.AddHours(-3));

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBe(LocalNow.AddHours(-3));
    }

    [Test]
    public async Task SaveChangesAsync_UploadedFileNeverChecked_StoresMinValue()
    {
        // Arrange
        var upload = AddUploadOfNewRelease(LocalNow.AddHours(-1), null);

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBe(DateTime.MinValue);
    }

    [Test]
    public async Task SaveChangesAsync_UploadedFileNeverChecked_StoresSameValueAsMigrationBackfill()
    {
        // Arrange
        AddUploadOfNewRelease((DateTime?)null);
        var countUploadsWithNeverCheckedValueOfMigrationSql =
            DatabaseProvider == DatabaseProvider.Postgres
                ? """SELECT COUNT(*) AS "Value" FROM "Uploads" WHERE "OldestUploadedFileCheckedAt" = '-infinity'"""
                : """SELECT COUNT(*) AS "Value" FROM "Uploads" WHERE "OldestUploadedFileCheckedAt" = '0001-01-01 00:00:00'""";

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        var uploadsWithMigrationBackfillValueCount = await ReadDbContext
            .Database.SqlQueryRaw<int>(countUploadsWithNeverCheckedValueOfMigrationSql)
            .SingleAsync();
        uploadsWithMigrationBackfillValueCount.ShouldBe(1);
    }

    [Test]
    public async Task SaveChangesAsync_UploadedFileAddedToExistingUpload_UpdatesOldestCheckedAt()
    {
        // Arrange
        var upload = AddUploadOfNewRelease(LocalNow.AddHours(-1));
        await DbContext.SaveChangesAsync();
        var archive = testData.AddArchive(upload.UploadConfig.ArchiveConfig!, 1);
        testData.AddUploadedFile(upload, archive.ArchiveFiles[0], LocalNow.AddHours(-2));

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBe(LocalNow.AddHours(-2));
    }

    [Test]
    public async Task SaveChangesAsync_UploadedFileCheckedAtChanged_UpdatesOldestCheckedAt()
    {
        // Arrange
        var upload = AddUploadOfNewRelease(null, LocalNow.AddHours(-2));
        await DbContext.SaveChangesAsync();
        upload.UploadedFiles[0].CheckedAt = LocalNow;

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBe(LocalNow.AddHours(-2));
    }

    [Test]
    public async Task SaveChangesAsync_UploadedFilesDeleted_UpdatesOldestCheckedAtAndStoresNullWithoutFiles()
    {
        // Arrange
        var upload = AddUploadOfNewRelease(null, LocalNow.AddHours(-2));
        await DbContext.SaveChangesAsync();
        DbContext.UploadedFiles.Remove(upload.UploadedFiles[0]);
        await DbContext.SaveChangesAsync();
        var oldestCheckedAtWithOneFile = await GetStoredOldestUploadedFileCheckedAtAsync(upload);
        DbContext.UploadedFiles.Remove(upload.UploadedFiles.Single());

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        oldestCheckedAtWithOneFile.ShouldBe(LocalNow.AddHours(-2));
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBeNull();
    }

    [Test]
    public async Task SaveChangesAsync_UploadedFileMovedToOtherUpload_UpdatesBothUploads()
    {
        // Arrange
        var sourceUpload = AddUploadOfNewRelease(LocalNow.AddHours(-1), null);
        var targetUpload = AddUploadOfNewRelease(LocalNow.AddHours(-2));
        await DbContext.SaveChangesAsync();
        var movedUploadedFile = sourceUpload.UploadedFiles.Single(file => file.CheckedAt == null);
        movedUploadedFile.Upload = targetUpload;

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOldestUploadedFileCheckedAtAsync(sourceUpload)).ShouldBe(
            LocalNow.AddHours(-1)
        );
        (await GetStoredOldestUploadedFileCheckedAtAsync(targetUpload)).ShouldBe(DateTime.MinValue);
    }

    [Test]
    public async Task SaveChangesAsync_HosterRegistrationDeletedWithoutLoadedUploadConfigs_UpdatesReleaseOnlineStateAfterDatabaseCascade()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        testData.AddUpload(
            testData.AddUploadConfig(release, testData.AddHosterRegistration("Kept")),
            OnlineState.Online
        );
        var deletedHosterRegistration = testData.AddHosterRegistration("Deleted");
        testData.AddUploadConfig(release, deletedHosterRegistration);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        DbContext.HosterRegistrations.Remove(
            await DbContext.HosterRegistrations.SingleAsync(registration =>
                registration.Id == deletedHosterRegistration.Id
            )
        );

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task SaveChangesAsync_HosterRegistrationHoldingUploadOfUploadConfigOfOtherHosterRegistrationDeleted_UpdatesReleaseOnlineStateAfterDatabaseCascade()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var deletedHosterRegistration = testData.AddHosterRegistration("Deleted");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, testData.AddHosterRegistration("Kept")),
            OnlineState.Online
        );
        upload.HosterRegistration = deletedHosterRegistration;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        DbContext.HosterRegistrations.Remove(
            await DbContext.HosterRegistrations.SingleAsync(registration =>
                registration.Id == deletedHosterRegistration.Id
            )
        );

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Offline);
    }

    [Test]
    public async Task SaveChangesAsync_ArchiveConfigDeletedWithoutLoadedUploadConfigs_UpdatesReleaseOnlineStateAfterDatabaseCascade()
    {
        // Arrange
        var hosterRegistration = testData.AddHosterRegistration("Alpha");
        var release = testData.AddRelease("Release");
        var deletedArchiveConfig = testData.AddArchiveConfig(release);
        testData.AddUpload(
            testData.AddUploadConfig(release, hosterRegistration),
            OnlineState.Online
        );
        testData.AddUploadConfig(release, hosterRegistration, deletedArchiveConfig);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        DbContext.ArchiveConfigs.Remove(
            await DbContext.ArchiveConfigs.SingleAsync(archiveConfig =>
                archiveConfig.Id == deletedArchiveConfig.Id
            )
        );

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task SaveChangesAsync_ArchiveDeletedWithoutLoadedUploadedFiles_UpdatesOldestCheckedAtAfterDatabaseCascade()
    {
        // Arrange
        var upload = AddUploadOfNewRelease(LocalNow.AddHours(-1));
        var deletedArchive = testData.AddArchive(upload.UploadConfig.ArchiveConfig!, 1);
        testData.AddUploadedFile(upload, deletedArchive.ArchiveFiles[0], null);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        DbContext.Archives.Remove(
            await DbContext.Archives.SingleAsync(archive => archive.Id == deletedArchive.Id)
        );

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBe(LocalNow.AddHours(-1));
    }

    [Test]
    public async Task SaveChangesAsync_ArchiveFileDeletedWithoutLoadedUploadedFile_UpdatesOldestCheckedAtAfterDatabaseCascade()
    {
        // Arrange
        var upload = AddUploadOfNewRelease(LocalNow.AddHours(-1), null);
        await DbContext.SaveChangesAsync();
        var deletedArchiveFileId = upload
            .UploadedFiles.Single(file => file.CheckedAt == null)
            .ArchiveFileId;
        DbContext.ChangeTracker.Clear();
        DbContext.ArchiveFiles.Remove(
            await DbContext.ArchiveFiles.SingleAsync(archiveFile =>
                archiveFile.Id == deletedArchiveFileId
            )
        );

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBe(LocalNow.AddHours(-1));
    }

    [Test]
    public async Task SaveChangesAsync_OuterTransactionCommitted_StoresValuesTogetherWithTheChanges()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, testData.AddHosterRegistration("Alpha")),
            OnlineState.Online,
            LocalNow.AddHours(-1)
        );
        await DbContext.SaveChangesAsync();
        await using var outerTransaction = await DbContext.Database.BeginTransactionAsync();
        upload.OnlineState = OnlineState.Offline;
        upload.UploadedFiles[0].CheckedAt = LocalNow;

        // Act
        await DbContext.SaveChangesAsync();
        var onlineStateInsideOuterTransaction = await DbContext
            .Releases.Where(r => r.Id == release.Id)
            .Select(r => r.OnlineState)
            .SingleAsync();
        var onlineStateOutsideOuterTransaction = await GetStoredOnlineStateAsync(release);
        await outerTransaction.CommitAsync();

        // Assert
        DbContext.Database.CurrentTransaction.ShouldBeNull();
        onlineStateInsideOuterTransaction.ShouldBe(OnlineState.Offline);
        onlineStateOutsideOuterTransaction.ShouldBe(OnlineState.Online);
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Offline);
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBe(LocalNow);
    }

    [Test]
    public async Task SaveChangesAsync_OuterTransactionRolledBack_KeepsPreviousValues()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, testData.AddHosterRegistration("Alpha")),
            OnlineState.Online,
            LocalNow.AddHours(-1)
        );
        await DbContext.SaveChangesAsync();
        await using var outerTransaction = await DbContext.Database.BeginTransactionAsync();
        upload.OnlineState = OnlineState.Offline;
        upload.UploadedFiles[0].CheckedAt = LocalNow;

        // Act
        await DbContext.SaveChangesAsync();
        await outerTransaction.RollbackAsync();

        // Assert
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Online);
        (await GetStoredOldestUploadedFileCheckedAtAsync(upload)).ShouldBe(LocalNow.AddHours(-1));
        (
            await ReadDbContext
                .Uploads.Where(u => u.Id == upload.Id)
                .Select(u => u.OnlineState)
                .SingleAsync()
        ).ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task SaveChangesAsync_SaveFails_RollsBackStartedTransactionAndNextSaveStoresValues()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var uploadConfig = testData.AddUploadConfig(
            release,
            testData.AddHosterRegistration("Alpha")
        );
        var upload = testData.AddUpload(uploadConfig, OnlineState.Online);
        await DbContext.SaveChangesAsync();
        upload.OnlineState = OnlineState.Offline;
        var uploadWithMissingUploadConfig = new Upload
        {
            UploadConfigId = uploadConfig.Id + 1000,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            CreatedAt = LocalNow,
            UploadState = UploadState.Pending,
            OnlineState = OnlineState.Online,
        };
        DbContext.Uploads.Add(uploadWithMissingUploadConfig);

        // Act
        await Should.ThrowAsync<Exception>(() => DbContext.SaveChangesAsync());
        var onlineStateAfterFailedSave = await GetStoredOnlineStateAsync(release);
        var transactionAfterFailedSave = DbContext.Database.CurrentTransaction;
        DbContext.Entry(uploadWithMissingUploadConfig).State = EntityState.Detached;
        await DbContext.SaveChangesAsync();

        // Assert
        onlineStateAfterFailedSave.ShouldBe(OnlineState.Online);
        transactionAfterFailedSave.ShouldBeNull();
        DbContext.Database.AutoSavepointsEnabled.ShouldBeTrue();
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Offline);
    }

    [Test]
    public async Task SaveChangesAsync_ChangedUploadWasDeletedConcurrently_RollsBackStartedTransaction()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, testData.AddHosterRegistration("Alpha")),
            OnlineState.Online
        );
        await DbContext.SaveChangesAsync();
        await using var otherDbContext = CreateDbContext();
        otherDbContext.Uploads.Remove(
            await otherDbContext.Uploads.SingleAsync(u => u.Id == upload.Id)
        );
        await otherDbContext.SaveChangesAsync();
        upload.OnlineState = OnlineState.Offline;

        // Act
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => DbContext.SaveChangesAsync());

        // Assert
        DbContext.Database.CurrentTransaction.ShouldBeNull();
        (await GetStoredOnlineStateAsync(release)).ShouldBe(OnlineState.Offline);
    }

    [Test]
    public void SaveChanges_Synchronous_ThrowsNotSupportedException()
    {
        // Arrange
        testData.AddRelease("Release");

        // Act & Assert
        Should.Throw<NotSupportedException>(() => DbContext.SaveChanges());
    }

    private Upload AddUploadOfNewRelease(params DateTime?[] fileCheckedAts)
    {
        var release = testData.AddRelease($"Release {Guid.NewGuid()}");
        var uploadConfig = testData.AddUploadConfig(
            release,
            testData.AddHosterRegistration($"Hoster {Guid.NewGuid()}")
        );

        return testData.AddUpload(uploadConfig, OnlineState.Online, fileCheckedAts);
    }

    private async Task<OnlineState> GetStoredOnlineStateAsync(Release release)
    {
        return await ReadDbContext
            .Releases.Where(r => r.Id == release.Id)
            .Select(r => r.OnlineState)
            .SingleAsync();
    }

    private async Task<DateTime?> GetStoredOldestUploadedFileCheckedAtAsync(Upload upload)
    {
        return await ReadDbContext
            .Uploads.Where(u => u.Id == upload.Id)
            .Select(u => u.OldestUploadedFileCheckedAt)
            .SingleAsync();
    }
}
