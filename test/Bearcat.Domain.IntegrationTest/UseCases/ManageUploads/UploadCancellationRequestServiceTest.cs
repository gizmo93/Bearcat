using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploads;

public class UploadCancellationRequestServiceTest(DatabaseProvider databaseProvider)
    : UploadIntegrationTest(databaseProvider)
{
    private UploadCancellationRequestService service = null!;

    [SetUp]
    public void SetUpService()
    {
        var uploadCancellationService = new UploadCancellationService(CreateNotificationService());

        service = new UploadCancellationRequestService(
            new UploadFilesRepository(DbContext, DbContext, NoOpSecretProtector.Instance),
            new UploadFinalizationService(
                CreateTimeProvider(),
                Mock.Of<ILogger<UploadFinalizationService>>(),
                CreateNotificationService(),
                uploadCancellationService
            ),
            uploadCancellationService
        );
    }

    [Test]
    public async Task ApplyCancellationRequestsToUploadsInProgressAsync_ContextHasNoOpenWork_CancelsAndRemovesContext()
    {
        // Arrange
        var upload = await AddUploadWithCancellationRequestAsync();
        var context = new UploadExecutionContext(
            upload,
            totalFileCount: 1,
            successfulFileCount: 0,
            failedFileCount: 0,
            plannedFiles: [],
            CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None)
        );
        var uploadsInProgress = new UploadsInProgress(
            Mock.Of<ITransferProgressTracker>(),
            Mock.Of<ILogger>()
        );
        uploadsInProgress.StartUploading(context);

        // Act
        await service.ApplyCancellationRequestsToUploadsInProgressAsync(
            uploadsInProgress,
            CancellationToken.None
        );

        // Assert
        uploadsInProgress.ContextsByUploadId.ShouldBeEmpty();
        upload.UploadState.ShouldBe(UploadState.Canceled);
        upload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    private async Task<Upload> AddUploadWithCancellationRequestAsync()
    {
        var uploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads: false);
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.CancellationRequested,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
            UploadedFiles = [],
        };

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

        return upload;
    }
}
