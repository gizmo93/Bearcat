using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.Extraction;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.SfvVerification;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders.VerificationAndExtraction;

public class ReleaseFolderVerificationAndExtractionService(
    SfvChecksumVerifier sfvChecksumVerifier,
    FolderArchiveExtractionService archiveExtractionService,
    IFileSystemService fileSystemService,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<ReleaseFolderVerificationAndExtractionService> logger
)
{
    public async Task<bool> TryVerifyAndExtractAsync(
        ReleaseFolderObservation observation,
        CancellationToken cancellationToken
    )
    {
        archiveExtractionService.DeleteTemporaryExtractionFolders(observation.FolderPath);

        try
        {
            var sfvFiles = await VerifySfvFilesAsync(observation, cancellationToken);
            await ExtractArchivesAsync(observation, sfvFiles, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            archiveExtractionService.DeleteTemporaryExtractionFolders(observation.FolderPath);
            await MarkAsFailedAndNotifyAsync(observation, exception.Message, cancellationToken);

            return false;
        }

        return true;
    }

    private async Task<IReadOnlyList<SfvFile>> VerifySfvFilesAsync(
        ReleaseFolderObservation observation,
        CancellationToken cancellationToken
    )
    {
        var sfvFilePaths = SfvChecksumVerifier.FindSfvFiles(observation.FolderPath);

        if (sfvFilePaths.Count == 0)
        {
            return [];
        }

        logger.LogInformation(
            "Verifying {SfvFileCount} SFV files of release folder {FolderPath}",
            sfvFilePaths.Count,
            observation.FolderPath
        );

        return await sfvChecksumVerifier.VerifyAsync(
            folderPath: observation.FolderPath,
            sfvFilePaths: sfvFilePaths,
            transferIdentifier: new TransferIdentifier(
                TransferType.ReleaseFolderVerification,
                observation.Id
            ),
            sourceName: FolderPathHelper.GetFolderName(observation.FolderPath),
            cancellationToken: cancellationToken
        );
    }

    private async Task ExtractArchivesAsync(
        ReleaseFolderObservation observation,
        IReadOnlyList<SfvFile> sfvFiles,
        CancellationToken cancellationToken
    )
    {
        var extractedArchives = await archiveExtractionService.ExtractAllArchivesAsync(
            folderPath: observation.FolderPath,
            sfvFiles: sfvFiles,
            transferIdentifier: new TransferIdentifier(
                TransferType.ReleaseFolderExtraction,
                observation.Id
            ),
            sourceName: FolderPathHelper.GetFolderName(observation.FolderPath),
            cancellationToken: cancellationToken
        );

        if (extractedArchives.Count == 0)
        {
            logger.LogInformation(
                "Found no archives to extract in release folder {FolderPath}",
                observation.FolderPath
            );

            return;
        }

        logger.LogInformation(
            "Extracted {ArchiveCount} archives in release folder {FolderPath} and deleted their volumes",
            extractedArchives.Count,
            observation.FolderPath
        );
    }

    private async Task MarkAsFailedAndNotifyAsync(
        ReleaseFolderObservation observation,
        string errorMessage,
        CancellationToken cancellationToken
    )
    {
        var message =
            $"{errorMessage.TrimEnd('.')}. The files were kept in {observation.FolderPath}.";

        var fileCountAndSize = fileSystemService.GetFolderFileCountAndSize(observation.FolderPath);

        observation.FileCount = fileCountAndSize.FileCount;
        observation.TotalBytes = fileCountAndSize.TotalBytes;
        observation.ExtractionErrorMessage = message;
        observation.ExtractionFailedAt = timeProvider.GetLocalNow();

        await notificationService.CreateAsync(
            NotificationKind.ReleaseFolderExtractionFailed,
            $"Verifying and extracting the release folder '{FolderPathHelper.GetFolderName(observation.FolderPath)}' failed, no release was created: {message}",
            cancellationToken
        );

        logger.LogWarning(
            "Verifying and extracting the release folder {FolderPath} failed: {ErrorMessage}",
            observation.FolderPath,
            message
        );
    }
}
