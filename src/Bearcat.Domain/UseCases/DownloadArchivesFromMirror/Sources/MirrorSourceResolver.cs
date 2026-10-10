using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;

public class MirrorSourceResolver(IHosterFactory hosterFactory)
{
    public MirrorSourcePlan ResolveSources(
        IReadOnlyList<Upload> uploadsOfArchive,
        IReadOnlyList<ArchiveFile> neededArchiveFiles
    )
    {
        var neededArchiveFileIds = neededArchiveFiles.Select(file => file.Id).ToHashSet();

        var candidatesPerArchiveFileId = new Dictionary<int, List<MirrorSource>>();

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

            var newestUploadedFiles = upload
                .UploadedFiles.Where(uploadedFile =>
                    neededArchiveFileIds.Contains(uploadedFile.ArchiveFileId)
                )
                .GroupBy(uploadedFile => uploadedFile.ArchiveFileId)
                .Select(group => group.MaxBy(uploadedFile => uploadedFile.Id)!)
                .Where(uploadedFile =>
                    uploadedFile.OnlineState == OnlineState.Online
                    && !string.IsNullOrWhiteSpace(uploadedFile.HosterFileLink)
                );

            foreach (var uploadedFile in newestUploadedFiles)
            {
                if (
                    !candidatesPerArchiveFileId.TryGetValue(
                        uploadedFile.ArchiveFileId,
                        out var candidates
                    )
                )
                {
                    candidates = [];
                    candidatesPerArchiveFileId[uploadedFile.ArchiveFileId] = candidates;
                }

                candidates.Add(
                    new MirrorSource(
                        Registration: registration,
                        Upload: upload,
                        UploadedFile: uploadedFile
                    )
                );
            }
        }

        var sourcesPerArchiveFileId = candidatesPerArchiveFileId.ToDictionary(
            entry => entry.Key,
            IReadOnlyList<MirrorSource> (entry) =>
                entry
                    .Value.OrderBy(source => source.Registration.MirrorPriority)
                    .ThenByDescending(source => source.UploadedFile.CheckedAt)
                    .ThenByDescending(source => source.UploadedFile.Id)
                    .ToList()
        );

        var filesWithoutSource = neededArchiveFiles
            .Where(file => !sourcesPerArchiveFileId.ContainsKey(file.Id))
            .ToList();

        return new MirrorSourcePlan(
            SourcesPerArchiveFileId: sourcesPerArchiveFileId,
            FilesWithoutSource: filesWithoutSource
        );
    }
}
