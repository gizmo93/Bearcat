using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageArchives.Reuploads;

public static class ArchiveFilesToUpload
{
    public static List<UploadedFile> GetOnlineUploadedFilesToCarryOver(
        Upload upload,
        IReadOnlyList<Upload> uploadsOfArchive
    )
    {
        if (upload.UploadConfig.HosterRegistration.AlwaysReuploadAllFiles)
        {
            return [];
        }

        return uploadsOfArchive
            .Where(previousUpload =>
                previousUpload.Id != upload.Id
                && previousUpload.UploadConfigId == upload.UploadConfigId
            )
            .SelectMany(previousUpload => previousUpload.UploadedFiles)
            .GroupBy(uploadedFile => uploadedFile.ArchiveFileId)
            .Select(group => group.MaxBy(uploadedFile => uploadedFile.UploadId)!)
            .Where(uploadedFile =>
                uploadedFile.OnlineState == OnlineState.Online
                && !string.IsNullOrWhiteSpace(uploadedFile.HosterFileLink)
            )
            .ToList();
    }

    public static List<ArchiveFile> GetArchiveFilesToUpload(
        IReadOnlyList<ArchiveFile> archiveFiles,
        IReadOnlyList<Upload> uploads,
        IReadOnlyList<Upload> uploadsOfArchive
    )
    {
        var archiveFileIdsToUpload = new HashSet<int>();

        foreach (var upload in uploads)
        {
            var carriedOverArchiveFileIds = GetOnlineUploadedFilesToCarryOver(
                    upload: upload,
                    uploadsOfArchive: uploadsOfArchive
                )
                .Select(uploadedFile => uploadedFile.ArchiveFileId)
                .ToHashSet();

            archiveFileIdsToUpload.UnionWith(
                archiveFiles
                    .Where(archiveFile => !carriedOverArchiveFileIds.Contains(archiveFile.Id))
                    .Select(archiveFile => archiveFile.Id)
            );
        }

        return archiveFiles
            .Where(archiveFile => archiveFileIdsToUpload.Contains(archiveFile.Id))
            .ToList();
    }
}
