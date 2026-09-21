using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;

public class MirrorSourceResolver(IHosterFactory hosterFactory)
{
    public static IReadOnlyList<ArchiveFile> GetNeededArchiveFiles(
        Archive archive,
        IReadOnlyList<Upload> waitingUploads,
        IReadOnlyList<Upload> uploadsOfArchive
    )
    {
        var neededArchiveFileIds = new HashSet<int>();

        foreach (var upload in waitingUploads)
        {
            if (upload.UploadConfig.HosterRegistration.AlwaysReuploadAllFiles)
            {
                neededArchiveFileIds.UnionWith(archive.ArchiveFiles.Select(file => file.Id));
                continue;
            }

            var carriedOverArchiveFileIds = GetCarriedOverArchiveFileIds(
                newUpload: upload,
                previousUploads: uploadsOfArchive
            );

            neededArchiveFileIds.UnionWith(
                archive
                    .ArchiveFiles.Where(file => !carriedOverArchiveFileIds.Contains(file.Id))
                    .Select(file => file.Id)
            );
        }

        return archive.ArchiveFiles.Where(file => neededArchiveFileIds.Contains(file.Id)).ToList();
    }

    private static HashSet<int> GetCarriedOverArchiveFileIds(
        Upload newUpload,
        IReadOnlyList<Upload> previousUploads
    )
    {
        return previousUploads
            .Where(upload =>
                upload.Id != newUpload.Id && upload.UploadConfigId == newUpload.UploadConfigId
            )
            .SelectMany(upload => upload.UploadedFiles)
            .GroupBy(uploadedFile => uploadedFile.ArchiveFileId)
            .Select(group => group.MaxBy(uploadedFile => uploadedFile.UploadId)!)
            .Where(uploadedFile =>
                uploadedFile.OnlineState == OnlineState.Online
                && !string.IsNullOrWhiteSpace(uploadedFile.HosterFileLink)
            )
            .Select(uploadedFile => uploadedFile.ArchiveFileId)
            .ToHashSet();
    }

    public SelectedSourceUpload? FindSourceUpload(
        IReadOnlyList<Upload> uploadsOfArchive,
        IReadOnlyList<ArchiveFile> neededArchiveFiles
    )
    {
        var neededArchiveFileIds = neededArchiveFiles.Select(file => file.Id).ToHashSet();

        var candidates = new List<SelectedSourceUpload>();

        foreach (var upload in uploadsOfArchive)
        {
            var registration = upload.UploadConfig.HosterRegistration;

            if (!registration.IsActive || !registration.UseForMirrorDownloads)
            {
                continue;
            }

            if (hosterFactory.GetByName(registration.HosterClassName) is not IHosterWithDownload)
            {
                continue;
            }

            var newestFilePerArchiveFileId = upload
                .UploadedFiles.Where(uploadedFile =>
                    neededArchiveFileIds.Contains(uploadedFile.ArchiveFileId)
                )
                .GroupBy(uploadedFile => uploadedFile.ArchiveFileId)
                .Select(group => group.MaxBy(uploadedFile => uploadedFile.Id)!)
                .Where(uploadedFile =>
                    uploadedFile.OnlineState == OnlineState.Online
                    && !string.IsNullOrWhiteSpace(uploadedFile.HosterFileLink)
                )
                .ToDictionary(uploadedFile => uploadedFile.ArchiveFileId);

            if (newestFilePerArchiveFileId.Count != neededArchiveFileIds.Count)
            {
                continue;
            }

            candidates.Add(
                new SelectedSourceUpload(
                    Upload: upload,
                    UploadedFilesByArchiveFileId: newestFilePerArchiveFileId,
                    LastCheckedAt: newestFilePerArchiveFileId
                        .Values.Select(uploadedFile => uploadedFile.CheckedAt)
                        .Max(),
                    MirrorPriority: registration.MirrorPriority
                )
            );
        }

        return candidates
            .OrderBy(candidate => candidate.MirrorPriority)
            .ThenByDescending(candidate => candidate.LastCheckedAt)
            .FirstOrDefault();
    }
}
