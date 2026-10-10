using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.FolderConfirmation;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.Extraction;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.SfvVerification;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;

public class RemoteDownloadVerificationAndExtractionService(
    IRemoteDownloadVerificationAndExtractionRepository repository,
    SfvChecksumVerifier sfvChecksumVerifier,
    FolderArchiveExtractionService archiveExtractionService,
    FolderWriteCheck folderWriteCheck,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<RemoteDownloadVerificationAndExtractionService> logger
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await ResetDownloadsInterruptedByCrashAsync(cancellationToken);

        foreach (
            var download in await repository.GetDownloadsInDownloadedStateAsync(cancellationToken)
        )
        {
            await VerifyAndExtractAsync(download, cancellationToken);
        }
    }

    private async Task ResetDownloadsInterruptedByCrashAsync(CancellationToken cancellationToken)
    {
        foreach (
            var download in await repository.GetDownloadsInterruptedDuringVerificationOrExtractionAsync(
                cancellationToken
            )
        )
        {
            if (
                !await folderWriteCheck.IsWriteAllowedOtherwiseNotifyAsync(
                    download.LocalFolderPath,
                    cancellationToken
                )
            )
            {
                continue;
            }

            logger.LogInformation(
                "Resetting the remote download {DownloadId} in {LocalFolderPath} that was interrupted in state {State}",
                download.Id,
                download.LocalFolderPath,
                download.State
            );

            archiveExtractionService.DeleteTemporaryExtractionFolders(download.LocalFolderPath);
            download.State = RemoteSourceDownloadState.Downloaded;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task VerifyAndExtractAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken
    )
    {
        if (
            !await repository.TryRefreshAsync(download, cancellationToken)
            || download.State is not RemoteSourceDownloadState.Downloaded
        )
        {
            return;
        }

        if (
            !await folderWriteCheck.IsWriteAllowedOtherwiseNotifyAsync(
                download.LocalFolderPath,
                cancellationToken
            )
        )
        {
            return;
        }

        try
        {
            var sfvFiles = await VerifySfvFilesAsync(download, cancellationToken);

            if (download.ExtractArchivesBeforeReleaseCreation)
            {
                await ExtractArchivesAsync(download, sfvFiles, cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            archiveExtractionService.DeleteTemporaryExtractionFolders(download.LocalFolderPath);
            await MarkAsFailedAndNotifyAsync(download, exception.Message, cancellationToken);

            return;
        }

        await MarkAsReadyForReleaseCreationAsync(download, cancellationToken);
    }

    private async Task<IReadOnlyList<SfvFile>> VerifySfvFilesAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken
    )
    {
        var sfvFilePaths = SfvChecksumVerifier.FindSfvFiles(download.LocalFolderPath);

        if (sfvFilePaths.Count == 0)
        {
            return [];
        }

        download.State = RemoteSourceDownloadState.Verifying;
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Verifying {SfvFileCount} SFV files of remote download {DownloadId} in {LocalFolderPath}",
            sfvFilePaths.Count,
            download.Id,
            download.LocalFolderPath
        );

        return await sfvChecksumVerifier.VerifyAsync(
            folderPath: download.LocalFolderPath,
            sfvFilePaths: sfvFilePaths,
            transferIdentifier: new TransferIdentifier(
                TransferType.RemoteDownloadVerification,
                download.Id
            ),
            sourceName: download.SourceName,
            cancellationToken: cancellationToken
        );
    }

    private async Task ExtractArchivesAsync(
        RemoteSourceDownload download,
        IReadOnlyList<SfvFile> sfvFiles,
        CancellationToken cancellationToken
    )
    {
        download.State = RemoteSourceDownloadState.Extracting;
        await repository.SaveChangesAsync(cancellationToken);

        var extractedArchives = await archiveExtractionService.ExtractAllArchivesAsync(
            folderPath: download.LocalFolderPath,
            sfvFiles: sfvFiles,
            transferIdentifier: new TransferIdentifier(
                TransferType.RemoteDownloadExtraction,
                download.Id
            ),
            sourceName: download.SourceName,
            cancellationToken: cancellationToken
        );

        if (extractedArchives.Count == 0)
        {
            logger.LogInformation(
                "Found no archives to extract in remote download {DownloadId} in {LocalFolderPath}",
                download.Id,
                download.LocalFolderPath
            );

            return;
        }

        download.ArchivesExtractedAt = timeProvider.GetLocalNow();

        logger.LogInformation(
            "Extracted {ArchiveCount} archives of remote download {DownloadId} in {LocalFolderPath} and deleted their volumes",
            extractedArchives.Count,
            download.Id,
            download.LocalFolderPath
        );
    }

    private async Task MarkAsReadyForReleaseCreationAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken
    )
    {
        download.State = RemoteSourceDownloadState.ReadyForReleaseCreation;
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Remote download {DownloadId} in {LocalFolderPath} is ready for release creation",
            download.Id,
            download.LocalFolderPath
        );
    }

    private async Task MarkAsFailedAndNotifyAsync(
        RemoteSourceDownload download,
        string errorMessage,
        CancellationToken cancellationToken
    )
    {
        var message =
            $"{errorMessage.TrimEnd('.')}. The downloaded files were kept in {download.LocalFolderPath}.";

        download.State = RemoteSourceDownloadState.Failed;
        download.ErrorMessage = message;

        await notificationService.CreateAsync(
            NotificationKind.RemoteDownloadFailed,
            $"Remote download '{download.FolderName}' from {download.SourceName} failed: {message}",
            cancellationToken
        );

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Remote download {DownloadId} failed: {ErrorMessage}",
            download.Id,
            message
        );
    }
}
