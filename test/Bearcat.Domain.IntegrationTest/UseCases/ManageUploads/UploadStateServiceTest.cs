using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploads;

public class UploadStateServiceTest(DatabaseProvider databaseProvider)
    : UploadIntegrationTest(databaseProvider)
{
    private UploadStateService service = null!;

    [SetUp]
    public void SetUpService()
    {
        service = new UploadStateService(
            new UploadStateRepository(DbContext),
            CreateTimeProvider(),
            CreateNotificationService(),
            Mock.Of<ILogger<UploadStateService>>()
        );
    }

    [Test]
    public async Task SetUploadOfflineAsync_OnlineUpload_MarksUploadAndFilesOfflineAndCreatesWarning()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );

        // Act
        var result = await service.SetUploadOfflineAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        updated.OnlineState.ShouldBe(OnlineState.Offline);
        updated.NotFullyOnlineSince.ShouldNotBeNull();
        updated.FullyOfflineSince.ShouldNotBeNull();
        updated.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Offline);
        updated.UploadedFiles.ShouldAllBe(f => f.CheckedAt != null);
        updated.Notifications.Single().NotificationSeverity.ShouldBe(NotificationSeverity.Warning);
        updated.Notifications.Single().Message.ShouldBe("Upload manually marked as offline");
    }

    [Test]
    public async Task SetUploadOfflineAsync_PartiallyOnlineUpload_MarksUploadOffline()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await service.SetUploadOfflineAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        updated.OnlineState.ShouldBe(OnlineState.Offline);
        updated.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Offline);
    }

    [Test]
    public async Task SetUploadOfflineAsync_AlreadyOfflineUpload_ReturnsFalse()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await service.SetUploadOfflineAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task SetUploadOfflineAsync_UploadDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await service.SetUploadOfflineAsync(-1, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task CancelUploadAsync_UploadDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await service.CancelUploadAsync(-1, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task CancelUploadAsync_UploadAlreadyHasCancellationRequested_ReturnsTrue()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = UploadState.CancellationRequested;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.CancelUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task CancelUploadAsync_UploadCannotBeCanceled_ReturnsFalse()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await service.CancelUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    public async Task CancelUploadAsync_UploadCanBeCanceled_RequestsCancellation(
        UploadState uploadState
    )
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = uploadState;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.CancelUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updatedUpload = await DbContext.Uploads.Include(u => u.Notifications).SingleAsync();

        result.ShouldBeTrue();
        updatedUpload.UploadState.ShouldBe(UploadState.CancellationRequested);
        updatedUpload.Notifications.Single().Message.ShouldBe("Upload cancellation requested");
    }

    [Test]
    public async Task ResumeUploadAsync_UploadDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await service.ResumeUploadAsync(-1, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task ResumeUploadAsync_CanceledUpload_SetsUploadPending()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = UploadState.Canceled;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.ResumeUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updatedUpload = await DbContext.Uploads.SingleAsync();

        result.ShouldBeTrue();
        updatedUpload.UploadState.ShouldBe(UploadState.Pending);
    }

    [Test]
    public async Task ResumeUploadAsync_CanceledUploadWithoutArchive_SetsUploadWaitingForArchive()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads: false);
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Canceled,
            OnlineState = OnlineState.Unknown,
            UploadedFiles = [],
            ErrorMessages = [],
        };
        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.ResumeUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updatedUpload = await DbContext.Uploads.SingleAsync();

        result.ShouldBeTrue();
        updatedUpload.ArchiveId.ShouldBeNull();
        updatedUpload.UploadState.ShouldBe(UploadState.WaitingForArchive);
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.Completed)]
    [TestCase(UploadState.Failed)]
    [TestCase(UploadState.CancellationRequested)]
    public async Task ResumeUploadAsync_NonCanceledUpload_ReturnsFalseAndKeepsState(
        UploadState uploadState
    )
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = uploadState;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.ResumeUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updatedUpload = await DbContext.Uploads.SingleAsync();

        result.ShouldBeFalse();
        updatedUpload.UploadState.ShouldBe(uploadState);
    }

    [Test]
    public async Task DeleteUploadAsync_UploadDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await service.DeleteUploadAsync(-1, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Completed)]
    [TestCase(UploadState.Failed)]
    [TestCase(UploadState.Canceled)]
    public async Task DeleteUploadAsync_AllowedState_DeletesUpload(UploadState uploadState)
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        upload.UploadState = uploadState;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.DeleteUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();

        result.ShouldBeTrue();
        (await DbContext.Uploads.AnyAsync(u => u.Id == upload.Id)).ShouldBeFalse();
        (await DbContext.UploadedFiles.AnyAsync(f => f.UploadId == upload.Id)).ShouldBeFalse();
    }

    [TestCase(UploadState.WaitingForArchive)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.CancellationRequested)]
    public async Task DeleteUploadAsync_DisallowedState_DoesNotDeleteUpload(UploadState uploadState)
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = uploadState;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.DeleteUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();

        result.ShouldBeFalse();
        (await DbContext.Uploads.AnyAsync(u => u.Id == upload.Id)).ShouldBeTrue();
    }
}
