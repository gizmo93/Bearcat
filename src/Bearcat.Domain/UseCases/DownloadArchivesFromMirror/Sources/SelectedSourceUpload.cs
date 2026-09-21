using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;

public sealed record SelectedSourceUpload(
    Upload Upload,
    Dictionary<int, UploadedFile> UploadedFilesByArchiveFileId,
    DateTime? LastCheckedAt,
    int MirrorPriority
);
