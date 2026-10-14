using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class UploadConfigWriteRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime LocalNow = MaterializedValuesTestData.LocalNow;

    private UploadConfigWriteRepository repository = null!;
    private MaterializedValuesTestData testData = null!;

    [SetUp]
    public void Setup()
    {
        repository = new UploadConfigWriteRepository(DbContext);
        testData = new MaterializedValuesTestData(DbContext);
    }

    [Test]
    public async Task GetWaitingOrPendingUploadsWithoutUploadedFilesAsync_UploadsInEveryState_ReturnsOnlyWaitingOrPendingUploadsWithoutUploadedFiles()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var uploadConfig = testData.AddUploadConfig(
            release,
            testData.AddHosterRegistration("Alpha")
        );
        var waitingUpload = AddUpload(uploadConfig, UploadState.WaitingForArchive);
        var pendingUpload = AddUpload(uploadConfig, UploadState.Pending);
        AddUpload(uploadConfig, UploadState.Pending, LocalNow);
        AddUpload(uploadConfig, UploadState.Uploading);
        AddUpload(uploadConfig, UploadState.CancellationRequested);
        AddUpload(uploadConfig, UploadState.Canceled);
        AddUpload(uploadConfig, UploadState.Failed);
        AddUpload(uploadConfig, UploadState.Completed);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetWaitingOrPendingUploadsWithoutUploadedFilesAsync(
            uploadConfig.Id,
            CancellationToken.None
        );

        // Assert
        result.Select(upload => upload.Id).ShouldBe([waitingUpload.Id, pendingUpload.Id]);
    }

    [Test]
    public async Task GetWaitingOrPendingUploadsWithoutUploadedFilesAsync_WaitingUploadOfOtherUploadConfig_ReturnsEmpty()
    {
        // Arrange
        var release = testData.AddRelease("Release");
        var hosterRegistration = testData.AddHosterRegistration("Alpha");
        var uploadConfig = testData.AddUploadConfig(release, hosterRegistration);
        var otherUploadConfig = testData.AddUploadConfig(release, hosterRegistration);
        AddUpload(otherUploadConfig, UploadState.WaitingForArchive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetWaitingOrPendingUploadsWithoutUploadedFilesAsync(
            uploadConfig.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    private Upload AddUpload(
        UploadConfig uploadConfig,
        UploadState uploadState,
        params DateTime?[] fileCheckedAts
    )
    {
        var upload = testData.AddUpload(uploadConfig, OnlineState.Unknown, fileCheckedAts);
        upload.UploadState = uploadState;
        upload.UploadedAt = null;

        return upload;
    }
}
