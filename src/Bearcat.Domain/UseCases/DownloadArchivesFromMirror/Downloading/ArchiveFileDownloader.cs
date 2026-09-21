using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public class ArchiveFileDownloader(
    IDownloadProgressTracker downloadProgressTracker,
    ILogger<ArchiveFileDownloader> logger
)
{
    private const int MaxErrorMessageLength = 500;

    public async Task<FileDownloadResult> DownloadAndVerifyAsync(
        int archiveId,
        string hosterName,
        PlannedFileDownload download,
        IHosterWithDownload hoster,
        IHosterConfig hosterConfig,
        SemaphoreSlim semaphore,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken
    )
    {
        await semaphore.WaitAsync(cancellationToken);

        var fileName = Path.GetFileName(download.TargetFilePath);
        var maxAttempts = downloadSettings.MaxAttempts;
        var errorMessages = new HashSet<string>();
        var attemptsMade = 0;

        try
        {
            foreach (var attempt in Enumerable.Range(1, maxAttempts))
            {
                attemptsMade = attempt;

                var outcome = await RunDownloadAttemptAsync(
                    archiveId: archiveId,
                    hosterName: hosterName,
                    download: download,
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    cancellationToken: cancellationToken
                );

                if (outcome.ErrorMessage is null)
                {
                    if (attempt > 1)
                    {
                        logger.LogInformation(
                            "Download of {FileName} from hoster {HosterName} succeeded on attempt {Attempt} of {MaxAttempts}",
                            fileName,
                            hosterName,
                            attempt,
                            maxAttempts
                        );
                    }

                    return FileDownloadResult.Succeeded(
                        download: download,
                        hosterName: hosterName,
                        md5Hash: outcome.Md5Hash!,
                        attempts: attempt
                    );
                }

                logger.LogWarning(
                    outcome.Exception,
                    "Download attempt {Attempt} of {MaxAttempts} for archive file {ArchiveFileId} ({FileName}) of archive {ArchiveId} from hoster {HosterName} at {HosterFileLink} failed: {ErrorMessage}",
                    attempt,
                    maxAttempts,
                    download.ArchiveFileId,
                    fileName,
                    archiveId,
                    hosterName,
                    download.UploadedFile.HosterFileLink,
                    outcome.ErrorMessage
                );

                if (outcome.IsFileMissing)
                {
                    return FileDownloadResult.Failed(
                        download: download,
                        hosterName: hosterName,
                        attempts: attempt,
                        isFileMissing: true,
                        errorMessages:
                        [
                            $"The file does not exist on {hosterName} any more: {Truncate(outcome.ErrorMessage)}",
                        ]
                    );
                }

                errorMessages.Add(Truncate(outcome.ErrorMessage));

                if (attempt == maxAttempts || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                logger.LogInformation(
                    "Retrying the download of {FileName} from hoster {HosterName} in {RetryDelaySeconds} seconds (attempt {NextAttempt} of {MaxAttempts})",
                    fileName,
                    hosterName,
                    downloadSettings.RetryDelay.TotalSeconds,
                    attempt + 1,
                    maxAttempts
                );

                if (downloadSettings.RetryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(downloadSettings.RetryDelay, cancellationToken);
                }
            }

            return FileDownloadResult.Failed(
                download: download,
                hosterName: hosterName,
                attempts: attemptsMade,
                isFileMissing: false,
                errorMessages: errorMessages.ToList()
            );
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<AttemptOutcome> RunDownloadAttemptAsync(
        int archiveId,
        string hosterName,
        PlannedFileDownload download,
        IHosterWithDownload hoster,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await hoster.DownloadFileAsync(
                file: new DownloadFileDto(
                    HosterFileLink: download.UploadedFile.HosterFileLink,
                    ExternalId: download.UploadedFile.ExternalId,
                    ExpectedSizeBytes: download.ExpectedSizeBytes
                ),
                targetFilePath: download.TargetFilePath,
                hosterConfig: hosterConfig,
                progress: new DownloadProgressReporter(
                    tracker: downloadProgressTracker,
                    archiveId: archiveId,
                    archiveFileId: download.ArchiveFileId,
                    fileName: Path.GetFileName(download.TargetFilePath)
                ),
                cancellationToken: cancellationToken
            );

            if (!result.IsSuccess)
            {
                return new AttemptOutcome(
                    Md5Hash: null,
                    ErrorMessage: JoinErrorMessages(result.ErrorMessages),
                    Exception: null,
                    IsFileMissing: result.IsFileMissing
                );
            }

            return await VerifyDownloadAsync(
                download: download,
                hosterName: hosterName,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AttemptOutcome(
                Md5Hash: null,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message,
                Exception: ex,
                IsFileMissing: false
            );
        }
    }

    private static async Task<AttemptOutcome> VerifyDownloadAsync(
        PlannedFileDownload download,
        string hosterName,
        CancellationToken cancellationToken
    )
    {
        var actualHash = await Md5FileHash.ComputeAsync(download.TargetFilePath, cancellationToken);
        var expectedHash = download.UploadedFile.Md5Hash;

        if (
            expectedHash is not null
            && !string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase)
        )
        {
            var sizeOnDisk = new FileInfo(download.TargetFilePath).Length;

            return new AttemptOutcome(
                Md5Hash: null,
                ErrorMessage: $"MD5 mismatch for {Path.GetFileName(download.TargetFilePath)} downloaded from {hosterName}: expected {expectedHash}, got {actualHash} ({sizeOnDisk.Bytes().Humanize("0.0")} on disk)",
                Exception: null,
                IsFileMissing: false
            );
        }

        return new AttemptOutcome(
            Md5Hash: actualHash,
            ErrorMessage: null,
            Exception: null,
            IsFileMissing: false
        );
    }

    private static string JoinErrorMessages(IReadOnlyList<string> errorMessages)
    {
        var joined = string.Join(" | ", errorMessages.Where(message => message.Length > 0));

        return joined.Length > 0 ? joined : "The hoster did not report an error message";
    }

    private static string Truncate(string errorMessage)
    {
        return errorMessage.Length > MaxErrorMessageLength
            ? errorMessage[..MaxErrorMessageLength]
            : errorMessage;
    }

    private sealed record AttemptOutcome(
        string? Md5Hash,
        string? ErrorMessage,
        Exception? Exception,
        bool IsFileMissing
    );
}
