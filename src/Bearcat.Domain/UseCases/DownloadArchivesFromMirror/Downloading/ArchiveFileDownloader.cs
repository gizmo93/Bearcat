using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;
using Bearcat.Domain.ValueObjects;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public class ArchiveFileDownloader(
    ITransferProgressTracker transferProgressTracker,
    ILogger<ArchiveFileDownloader> logger
)
{
    private const int MaxErrorMessageLength = 500;

    public async Task<FileDownloadResult> DownloadAndVerifyAsync(
        int archiveId,
        PlannedFileDownload download,
        IReadOnlyDictionary<int, ResolvedMirrorHoster> hosters,
        SemaphoreSlim semaphore,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken
    )
    {
        await semaphore.WaitAsync(cancellationToken);

        var fileName = Path.GetFileName(download.TargetFilePath);
        var maxAttempts = downloadSettings.MaxAttempts;
        var sourceFailures = new List<SourceFailure>();

        try
        {
            for (var sourceIndex = 0; sourceIndex < download.Sources.Count; sourceIndex++)
            {
                var mirrorSource = download.Sources[sourceIndex];
                var resolvedMirrorHoster = hosters[mirrorSource.Registration.Id];
                var hosterName = mirrorSource.Registration.Name;

                if (sourceIndex > 0)
                {
                    logger.LogInformation(
                        "Falling back from hoster {PreviousHosterName} to {NextHosterName} for archive file {FileName} of archive {ArchiveId}",
                        download.Sources[sourceIndex - 1].Registration.Name,
                        hosterName,
                        fileName,
                        archiveId
                    );
                }

                var expectedSizeBytes =
                    sourceIndex == 0
                        ? download.ExpectedSizeBytes
                        : await GetFileSizeAsync(
                            resolved: resolvedMirrorHoster,
                            source: mirrorSource,
                            cancellationToken: cancellationToken
                        );

                var errorMessages = new HashSet<string>();
                var attemptsMade = 0;
                SourceFailure? missingFileFailure = null;

                foreach (var attempt in Enumerable.Range(1, maxAttempts))
                {
                    attemptsMade = attempt;

                    var outcome = await RunDownloadAttemptAsync(
                        archiveId: archiveId,
                        download: download,
                        source: mirrorSource,
                        resolved: resolvedMirrorHoster,
                        expectedSizeBytes: expectedSizeBytes,
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
                        mirrorSource.UploadedFile.HosterFileLink,
                        outcome.ErrorMessage
                    );

                    if (outcome.IsFileMissing)
                    {
                        mirrorSource.UploadedFile.OnlineState = OnlineState.Offline;
                        mirrorSource.UploadedFile.CheckedAt = DateTime.UtcNow;

                        missingFileFailure = new SourceFailure(
                            HosterName: hosterName,
                            HosterFileLink: mirrorSource.UploadedFile.HosterFileLink,
                            Attempts: attempt,
                            IsFileMissing: true,
                            ErrorMessages:
                            [
                                $"The file does not exist on {hosterName} any more: {Truncate(outcome.ErrorMessage)}",
                            ]
                        );

                        break;
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

                sourceFailures.Add(
                    missingFileFailure
                        ?? new SourceFailure(
                            HosterName: hosterName,
                            HosterFileLink: mirrorSource.UploadedFile.HosterFileLink,
                            Attempts: attemptsMade,
                            IsFileMissing: false,
                            ErrorMessages: errorMessages.ToList()
                        )
                );

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            return FileDownloadResult.Failed(download: download, sourceFailures: sourceFailures);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<long?> GetFileSizeAsync(
        ResolvedMirrorHoster resolved,
        MirrorSource source,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var sizePerFileUrl = await resolved.Hoster.GetFileSizesAsync(
                [source.UploadedFile.HosterFileLink],
                resolved.Config,
                cancellationToken
            );

            return sizePerFileUrl.TryGetValue(source.UploadedFile.HosterFileLink, out var sizeBytes)
                ? sizeBytes
                : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Could not determine the size of {HosterFileLink} on hoster {HosterName}, the download progress stays indeterminate",
                source.UploadedFile.HosterFileLink,
                source.Registration.Name
            );

            return null;
        }
    }

    private async Task<AttemptOutcome> RunDownloadAttemptAsync(
        int archiveId,
        PlannedFileDownload download,
        MirrorSource source,
        ResolvedMirrorHoster resolved,
        long? expectedSizeBytes,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await resolved.Hoster.DownloadFileAsync(
                file: new DownloadFileDto(
                    HosterFileLink: source.UploadedFile.HosterFileLink,
                    ExternalId: source.UploadedFile.ExternalId,
                    ExpectedSizeBytes: expectedSizeBytes
                ),
                targetFilePath: download.TargetFilePath,
                hosterConfig: resolved.Config,
                progress: new TransferProgressReporter(
                    tracker: transferProgressTracker,
                    identifier: new TransferIdentifier(TransferKind.MirrorDownload, archiveId),
                    fileId: download.ArchiveFileId,
                    fileName: Path.GetFileName(download.TargetFilePath),
                    sourceName: source.Registration.Name
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
                source: source,
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
        MirrorSource source,
        CancellationToken cancellationToken
    )
    {
        var actualHash = await Md5FileHash.ComputeAsync(download.TargetFilePath, cancellationToken);
        var expectedHash = source.UploadedFile.Md5Hash;

        if (
            expectedHash is not null
            && !string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase)
        )
        {
            var sizeOnDisk = new FileInfo(download.TargetFilePath).Length;

            return new AttemptOutcome(
                Md5Hash: null,
                ErrorMessage: $"MD5 mismatch for {Path.GetFileName(download.TargetFilePath)} downloaded from {source.Registration.Name}: expected {expectedHash}, got {actualHash} ({sizeOnDisk.Bytes().Humanize("0.0")} on disk)",
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
