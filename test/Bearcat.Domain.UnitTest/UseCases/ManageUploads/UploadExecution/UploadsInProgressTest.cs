using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads.UploadExecution;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageUploads.UploadExecution;

public class UploadsInProgressTest
{
    [Test]
    public async Task RemoveCompletedFileUploadTasksAndLogUnexpectedErrorsAsync_WorkerFails_LogsAndRemovesTask()
    {
        // Arrange
        var uploadsInProgress = CreateUploadsInProgress();
        uploadsInProgress.RunningFileUploadTasks.Add(
            Task.FromException(new InvalidOperationException("Worker failed"))
        );

        // Act
        await uploadsInProgress.RemoveCompletedFileUploadTasksAndLogUnexpectedErrorsAsync(
            CancellationToken.None
        );

        // Assert
        uploadsInProgress.RunningFileUploadTasks.ShouldBeEmpty();
    }

    [Test]
    public async Task RemoveCompletedFileUploadTasksAndLogUnexpectedErrorsAsync_ProcessCancellationWasRequested_RethrowsCancellation()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var uploadsInProgress = CreateUploadsInProgress();
        uploadsInProgress.RunningFileUploadTasks.Add(
            Task.FromCanceled(cancellationTokenSource.Token)
        );

        // Act / Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await uploadsInProgress.RemoveCompletedFileUploadTasksAndLogUnexpectedErrorsAsync(
                cancellationTokenSource.Token
            )
        );
    }

    [Test]
    public void GetUploadsWithPendingFilesInStartOrder_OldestFirstWithoutPrioritizedUploads_ReturnsUploadsWithRunningFilesFirstThenOldest()
    {
        // Arrange
        var uploadsInProgress = CreateUploadsInProgress(
            CreateContext(uploadId: 1),
            CreateContext(uploadId: 2, runningFileCount: 1),
            CreateContext(uploadId: 3, isReupload: true),
            CreateContext(uploadId: 4, pendingFileCount: 0, runningFileCount: 1)
        );

        // Act
        var result = uploadsInProgress.GetUploadsWithPendingFilesInStartOrder(
            UploadStartOrder.OldestFirst
        );

        // Assert
        result.Select(context => context.UploadId).ShouldBe([2, 1, 3]);
    }

    [Test]
    public void GetUploadsWithPendingFilesInStartOrder_OldestFirstWithPrioritizedUploads_ReturnsNewestPrioritizationFirstThenUploadsWithoutPrioritization()
    {
        // Arrange
        var uploadsInProgress = CreateUploadsInProgress(
            CreateContext(uploadId: 1, runningFileCount: 1),
            CreateContext(uploadId: 2, prioritizedAt: new DateTime(2026, 11, 16, 10, 0, 0)),
            CreateContext(uploadId: 3, prioritizedAt: new DateTime(2026, 11, 16, 11, 0, 0)),
            CreateContext(uploadId: 4)
        );

        // Act
        var result = uploadsInProgress.GetUploadsWithPendingFilesInStartOrder(
            UploadStartOrder.OldestFirst
        );

        // Assert
        result.Select(context => context.UploadId).ShouldBe([3, 2, 1, 4]);
    }

    [Test]
    public void GetUploadsWithPendingFilesInStartOrder_InitialUploadsBeforeReuploads_ReturnsUploadsWithRunningFilesFirstThenInitialUploadsThenReuploads()
    {
        // Arrange
        var uploadsInProgress = CreateUploadsInProgress(
            CreateContext(uploadId: 1, isReupload: true),
            CreateContext(uploadId: 2),
            CreateContext(uploadId: 3, isReupload: true, runningFileCount: 1),
            CreateContext(uploadId: 4)
        );

        // Act
        var result = uploadsInProgress.GetUploadsWithPendingFilesInStartOrder(
            UploadStartOrder.InitialUploadsBeforeReuploads
        );

        // Assert
        result.Select(context => context.UploadId).ShouldBe([3, 2, 4, 1]);
    }

    [Test]
    public void GetUploadsWithPendingFilesInStartOrder_InitialUploadsBeforeReuploadsWithPrioritizedReupload_ReturnsPrioritizedReuploadFirst()
    {
        // Arrange
        var uploadsInProgress = CreateUploadsInProgress(
            CreateContext(uploadId: 1, runningFileCount: 1),
            CreateContext(
                uploadId: 2,
                isReupload: true,
                prioritizedAt: new DateTime(2026, 11, 16, 10, 0, 0)
            ),
            CreateContext(uploadId: 3)
        );

        // Act
        var result = uploadsInProgress.GetUploadsWithPendingFilesInStartOrder(
            UploadStartOrder.InitialUploadsBeforeReuploads
        );

        // Assert
        result.Select(context => context.UploadId).ShouldBe([2, 1, 3]);
    }

    private static UploadsInProgress CreateUploadsInProgress(
        params UploadExecutionContext[] contexts
    )
    {
        var uploadsInProgress = CreateUploadsInProgress();

        foreach (var context in contexts)
        {
            uploadsInProgress.StartUploading(context);
        }

        return uploadsInProgress;
    }

    private static UploadExecutionContext CreateContext(
        int uploadId,
        bool isReupload = false,
        DateTime? prioritizedAt = null,
        int pendingFileCount = 1,
        int runningFileCount = 0
    )
    {
        var upload = new Upload
        {
            Id = uploadId,
            PrioritizedAt = prioritizedAt,
            UploadedFiles = [],
        };
        var context = new UploadExecutionContext(
            upload: upload,
            sourceFiles: [],
            successfulFileCount: 0,
            failedFileCount: 0,
            plannedFiles: [],
            isReupload: isReupload,
            cancellationTokenSource: new CancellationTokenSource()
        )
        {
            RunningFileCount = runningFileCount,
        };

        for (var fileIndex = 0; fileIndex < pendingFileCount; fileIndex++)
        {
            context.PendingFiles.Enqueue(
                new FileToUpload(
                    UploadId: uploadId,
                    SourceFile: new UploadSourceFile(
                        TransferFileId: fileIndex,
                        ArchiveFileId: fileIndex,
                        VideoFileRelativePath: null,
                        FullFileName: $"/tmp/archive.part{fileIndex}.rar",
                        Md5Hash: null
                    ),
                    FolderId: null,
                    HosterClassName: "TestHoster",
                    Hoster: Mock.Of<IHoster>(),
                    HosterConfig: Mock.Of<IHosterConfig>()
                )
            );
        }

        return context;
    }

    private static UploadsInProgress CreateUploadsInProgress()
    {
        return new UploadsInProgress(Mock.Of<ITransferProgressTracker>(), Mock.Of<ILogger>());
    }
}
