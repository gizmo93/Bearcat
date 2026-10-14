using System.Globalization;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;
using Bearcat.Domain.ValueObjects;
using Humanizer;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public class ArchiveFileDownloader(
    ITransferProgressTracker transferProgressTracker,
    TransferSpeedLimitService transferSpeedLimitService,
    MirrorFileSizeService mirrorFileSizeService,
    TimeProvider timeProvider,
    ILogger<ArchiveFileDownloader> logger
)
{
    private const int MaxErrorMessageLength = 500;

    public async Task<FileDownloadResult> DownloadAndVerifyAsync(
        int archiveId,
        PlannedFileDownload download,
        IReadOnlyDictionary<int, ResolvedMirrorHoster> hosters,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken
    )
    {
        var sourceFailures = new List<SourceFailure>();

        foreach (
            var (mirrorSource, sourceIndex) in download.Sources.Select(
                (mirrorSource, sourceIndex) => (mirrorSource, sourceIndex)
            )
        )
        {
            var resolvedMirrorHoster = hosters[mirrorSource.Registration.Id];

            if (sourceIndex > 0)
            {
                logger.LogInformation(
                    "Falling back from hoster {PreviousHosterName} to {NextHosterName} for archive file {FileName} of archive {ArchiveId}",
                    download.Sources[sourceIndex - 1].Registration.Name,
                    mirrorSource.Registration.Name,
                    Path.GetFileName(download.TargetFilePath),
                    archiveId
                );
            }

            var expectedSizeBytes = await GetExpectedSizeBytesAsync(
                download: download,
                sourceIndex: sourceIndex,
                mirrorSource: mirrorSource,
                resolvedMirrorHoster: resolvedMirrorHoster,
                cancellationToken: cancellationToken
            );

            var outcome = await DownloadFromSourceWithRetriesAsync(
                archiveId: archiveId,
                download: download,
                mirrorSource: mirrorSource,
                resolvedMirrorHoster: resolvedMirrorHoster,
                expectedSizeBytes: expectedSizeBytes,
                downloadSettings: downloadSettings,
                cancellationToken: cancellationToken
            );

            if (outcome.Failure is null)
            {
                return FileDownloadResult.Succeeded(
                    download: download,
                    hosterName: mirrorSource.Registration.Name,
                    md5Hash: outcome.Md5Hash!,
                    attempts: outcome.Attempts
                );
            }

            sourceFailures.Add(outcome.Failure);

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        return FileDownloadResult.Failed(download: download, sourceFailures: sourceFailures);
    }

    private async Task<long?> GetExpectedSizeBytesAsync(
        PlannedFileDownload download,
        int sourceIndex,
        MirrorSource mirrorSource,
        ResolvedMirrorHoster resolvedMirrorHoster,
        CancellationToken cancellationToken
    )
    {
        if (sourceIndex == 0)
        {
            return download.ExpectedSizeBytes;
        }

        var hosterFileLink = mirrorSource.UploadedFile.HosterFileLink;

        var sizePerHosterFileLink = await mirrorFileSizeService.GetFileSizesOrEmptyAsync(
            resolvedMirrorHoster: resolvedMirrorHoster,
            hosterFileLinks: [hosterFileLink],
            cancellationToken: cancellationToken
        );

        return sizePerHosterFileLink.TryGetValue(hosterFileLink, out var sizeBytes)
            ? sizeBytes
            : null;
    }

    private async Task<SourceDownloadOutcome> DownloadFromSourceWithRetriesAsync(
        int archiveId,
        PlannedFileDownload download,
        MirrorSource mirrorSource,
        ResolvedMirrorHoster resolvedMirrorHoster,
        long? expectedSizeBytes,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken
    )
    {
        var fileName = Path.GetFileName(download.TargetFilePath);
        var hosterName = mirrorSource.Registration.Name;
        var maxAttempts = downloadSettings.MaxAttempts;
        var errorMessages = new HashSet<string>();
        var attemptsMade = 0;

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

                return SourceDownloadOutcome.Succeeded(
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
                MarkUploadedFileOffline(mirrorSource);

                return SourceDownloadOutcome.Failed(
                    new SourceFailure(
                        HosterName: hosterName,
                        HosterFileLink: mirrorSource.UploadedFile.HosterFileLink,
                        Attempts: attempt,
                        IsFileMissing: true,
                        ErrorMessages:
                        [
                            $"The file does not exist on {hosterName} any more: {Truncate(outcome.ErrorMessage)}",
                        ]
                    )
                );
            }

            errorMessages.Add(Truncate(outcome.ErrorMessage));

            if (attempt == maxAttempts || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await WaitBeforeRetryAsync(
                fileName: fileName,
                hosterName: hosterName,
                attempt: attempt,
                downloadSettings: downloadSettings,
                cancellationToken: cancellationToken
            );
        }

        return SourceDownloadOutcome.Failed(
            new SourceFailure(
                HosterName: hosterName,
                HosterFileLink: mirrorSource.UploadedFile.HosterFileLink,
                Attempts: attemptsMade,
                IsFileMissing: false,
                ErrorMessages: errorMessages.ToList()
            )
        );
    }

    private void MarkUploadedFileOffline(MirrorSource source)
    {
        source.UploadedFile.OnlineState = OnlineState.Offline;
        source.UploadedFile.CheckedAt = timeProvider.GetLocalNow();
    }

    private async Task WaitBeforeRetryAsync(
        string fileName,
        string hosterName,
        int attempt,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Retrying the download of {FileName} from hoster {HosterName} in {RetryDelaySeconds} seconds (attempt {NextAttempt} of {MaxAttempts})",
            fileName,
            hosterName,
            downloadSettings.RetryDelay.TotalSeconds,
            attempt + 1,
            downloadSettings.MaxAttempts
        );

        if (downloadSettings.RetryDelay > TimeSpan.Zero)
        {
            await Task.Delay(downloadSettings.RetryDelay, cancellationToken);
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
            using var proxyScope = HosterRegistrationProxyScope.EnterForMirrorDownload(
                resolved.Registration
            );

            using var speedLimitScope = transferSpeedLimitService.EnterMirrorDownloadScope(
                resolved.Registration
            );

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
                    identifier: new TransferIdentifier(TransferType.MirrorDownload, archiveId),
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
                ErrorMessage: $"MD5 mismatch for {Path.GetFileName(download.TargetFilePath)} downloaded from {source.Registration.Name}: expected {expectedHash}, got {actualHash} ({sizeOnDisk.Bytes().Humanize("0.0", CultureInfo.InvariantCulture)} on disk)",
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

    private sealed record SourceDownloadOutcome(
        string? Md5Hash,
        int Attempts,
        SourceFailure? Failure
    )
    {
        public static SourceDownloadOutcome Succeeded(string md5Hash, int attempts)
        {
            return new SourceDownloadOutcome(Md5Hash: md5Hash, Attempts: attempts, Failure: null);
        }

        public static SourceDownloadOutcome Failed(SourceFailure failure)
        {
            return new SourceDownloadOutcome(
                Md5Hash: null,
                Attempts: failure.Attempts,
                Failure: failure
            );
        }
    }
}
