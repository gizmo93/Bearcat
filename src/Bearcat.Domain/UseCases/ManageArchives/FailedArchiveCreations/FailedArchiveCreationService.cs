using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.FailedArchiveCreations;

public class FailedArchiveCreationService(
    IArchiveCreationRepository repository,
    ILogger<FailedArchiveCreationService> logger,
    IFileSystemService fileSystemService,
    INotificationService notificationService
)
{
    public async Task MarkArchiveCreationFailedAndFailWaitingUploadsAsync(
        Archive archive,
        IReadOnlyList<string> errorMessages,
        CancellationToken cancellationToken
    )
    {
        logger.LogError(
            "Failed to create archive {ArchiveId} for ArchiveConfig {ArchiveConfigId}: {ErrorMessages}",
            archive.Id,
            archive.ArchiveConfig.Id,
            string.Join(", ", errorMessages)
        );

        archive.ArchiveState = ArchiveState.CreationFailed;
        archive.ErrorMessages.AddRange(errorMessages);

        notificationService.Create(
            kind: NotificationKind.ArchiveCreationFailed,
            message: $"Failed to create archive: {string.Join(", ", errorMessages)}",
            entity: archive,
            selector: n => n.Archive
        );

        archive.MarkWaitingUploadsAsFailedAndRemoveThemFromArchive();
        fileSystemService.DeleteDirectoryIfExists(archive.ArchiveFolderPath);

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    public async Task FailUploadsWaitingForCreationFailedArchivesAsync(
        CancellationToken cancellationToken
    )
    {
        var creationFailedArchives =
            await repository.GetCreationFailedArchivesWithWaitingUploadsAsync(cancellationToken);

        if (creationFailedArchives.Count == 0)
        {
            return;
        }

        foreach (var archive in creationFailedArchives)
        {
            logger.LogInformation(
                "Marking {UploadCount} uploads as failed because they still wait for archive {ArchiveId} whose creation failed",
                archive.Uploads.Count,
                archive.Id
            );

            archive.MarkWaitingUploadsAsFailedAndRemoveThemFromArchive();
        }

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteCreationFailedArchiveAsync(
        int archiveId,
        CancellationToken cancellationToken = default
    )
    {
        var archive = await repository.GetArchiveWithUploadsAsync(archiveId, cancellationToken);

        if (archive.ArchiveState is not ArchiveState.CreationFailed)
        {
            throw new InvalidOperationException(
                "Only archives whose creation failed can be deleted."
            );
        }

        logger.LogInformation(
            "Deleting archive {ArchiveId} whose creation failed and its folder {ArchiveFolderPath}",
            archive.Id,
            archive.ArchiveFolderPath
        );

        archive.MarkWaitingUploadsAsFailedAndRemoveThemFromArchive();
        fileSystemService.DeleteDirectoryIfExists(archive.ArchiveFolderPath);
        repository.Remove(archive);

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }
}
