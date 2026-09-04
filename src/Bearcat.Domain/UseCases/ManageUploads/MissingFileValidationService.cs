using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class MissingFileValidationService(
    IUploadFilesRepository repository,
    IFileSystemService fileSystemService,
    ILogger<MissingFileValidationService> logger,
    INotificationService notificationService
)
{
    public async Task<List<Upload>> GetUploadsWithMissingFilesAsync(
        IReadOnlyList<Upload> pendingUploads,
        CancellationToken cancellationToken
    )
    {
        var uploadsToSkip = new List<Upload>();

        foreach (var upload in pendingUploads)
        {
            var hasNonExistingFiles = await HandleNonExistingArchiveFilesAsync(
                upload,
                cancellationToken
            );

            if (hasNonExistingFiles)
            {
                uploadsToSkip.Add(upload);
            }
        }

        return uploadsToSkip;
    }

    private async Task<bool> HandleNonExistingArchiveFilesAsync(
        Upload upload,
        CancellationToken cancellationToken
    )
    {
        if (upload.Archive is null)
        {
            return false;
        }

        var nonExistingFiles = upload
            .Archive!.ArchiveFiles.Where(af =>
                !fileSystemService.FileExists(af.FullFileName)
                || af.Archive.ArchiveState != ArchiveState.Created
            )
            .ToList();

        if (nonExistingFiles.Count == 0)
        {
            return false;
        }

        await HandleMissingFilesAsync(upload, nonExistingFiles, cancellationToken);

        return true;
    }

    private async Task HandleMissingFilesAsync(
        Upload upload,
        List<ArchiveFile> nonExistingFiles,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "The following archive files for Upload {UploadId} do not exist: {FilePaths}",
            upload.Id,
            string.Join(", ", nonExistingFiles.Select(f => f.FullFileName))
        );

        notificationService.Create(
            kind: NotificationKind.ArchiveFilesMissing,
            message: GetMissingFilesNotificationMessage(upload.UploadConfig.Release.ReleaseType),
            entity: upload,
            selector: n => n.Upload
        );

        if (upload.Archive!.ArchiveState == ArchiveState.Created)
        {
            upload.Archive.ArchiveState = ArchiveState.MissingFiles;
        }

        upload.ArchiveId = null;
        upload.UploadState = UploadState.WaitingForArchive;
        upload.UploadedFiles = [];

        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string GetMissingFilesNotificationMessage(ReleaseType releaseType)
    {
        return releaseType switch
        {
            ReleaseType.Managed =>
                "The archive assigned upload has missing files, triggering re-packaging",
            ReleaseType.Unmanaged =>
                "The archive assigned upload has missing files. Refresh the unmanaged archive after providing the archive files.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(releaseType),
                $"Unknown release type, {releaseType}"
            ),
        };
    }
}
