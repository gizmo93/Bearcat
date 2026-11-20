using Bearcat.Abstractions.Transfers;

namespace Bearcat.Domain.Shared.Transfers;

public static class FolderSizeProgressReporter
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);

    public static async Task<TResult> RunWhileReportingFolderSizeAsync<TResult>(
        string folderPath,
        ITransferProgress progress,
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        using var pollingCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );

        var pollingTask = ReportFolderSizePeriodicallyAsync(
            folderPath: folderPath,
            progress: progress,
            cancellationToken: pollingCancellationSource.Token
        );

        try
        {
            return await operation();
        }
        finally
        {
            await pollingCancellationSource.CancelAsync();
            await pollingTask;
        }
    }

    private static async Task ReportFolderSizePeriodicallyAsync(
        string folderPath,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        using var timer = new PeriodicTimer(PollingInterval);
        var reportedBytes = 0L;

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                reportedBytes = ReportFolderSizeIncrease(folderPath, reportedBytes, progress);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ReportFolderSizeIncrease(folderPath, reportedBytes, progress);
        }
    }

    private static long ReportFolderSizeIncrease(
        string folderPath,
        long reportedBytes,
        ITransferProgress progress
    )
    {
        var folderSizeBytes = new DirectoryInfo(folderPath)
            .EnumerateFiles(
                "*",
                new EnumerationOptions
                {
                    AttributesToSkip = FileAttributes.None,
                    RecurseSubdirectories = true,
                }
            )
            .Sum(file => file.Length);

        progress.ReportBytesTransferred(folderSizeBytes - reportedBytes);

        return folderSizeBytes;
    }
}
