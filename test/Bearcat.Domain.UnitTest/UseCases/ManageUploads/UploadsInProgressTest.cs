using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageUploads;

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

    private static UploadsInProgress CreateUploadsInProgress()
    {
        return new UploadsInProgress(Mock.Of<ITransferProgressTracker>(), Mock.Of<ILogger>());
    }
}
