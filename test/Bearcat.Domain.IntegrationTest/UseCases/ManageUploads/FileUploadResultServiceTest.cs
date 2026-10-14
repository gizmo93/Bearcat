using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploads;

public class FileUploadResultServiceTest
{
    [Test]
    public async Task ApplyFileUploadResultAsync_ContextDoesNotExist_IgnoresResult()
    {
        // Arrange
        var service = CreateFileUploadResultService();
        var uploadsInProgress = new UploadsInProgress(
            Mock.Of<ITransferProgressTracker>(),
            Mock.Of<ILogger>()
        );
        var result = new FileUploadCompleted(
            UploadId: 42,
            ArchiveFileId: 100,
            FullFileName: "/tmp/archive.part1.rar",
            FileUrl: null,
            ExternalId: null,
            IsSuccess: false,
            Errors: ["Ignored"]
        );

        // Act
        await service.ApplyFileUploadResultAsync(result, uploadsInProgress, CancellationToken.None);

        // Assert
        uploadsInProgress.ContextsByUploadId.ShouldBeEmpty();
    }

    private static FileUploadResultService CreateFileUploadResultService()
    {
        var timeProvider = new TimeProvider(new ConfigurationBuilder().Build());
        var notificationService = Mock.Of<INotificationService>();

        return new FileUploadResultService(
            new Mock<IUploadFilesRepository>(MockBehavior.Strict).Object,
            timeProvider,
            new UploadFinalizationService(
                timeProvider,
                Mock.Of<ILogger<UploadFinalizationService>>(),
                notificationService,
                new UploadCancellationService(notificationService)
            ),
            Mock.Of<ILogger<FileUploadResultService>>()
        );
    }
}
