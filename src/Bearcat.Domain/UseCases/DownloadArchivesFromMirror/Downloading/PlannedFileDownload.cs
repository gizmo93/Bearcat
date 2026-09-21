using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public sealed record PlannedFileDownload(
    int ArchiveFileId,
    string TargetFilePath,
    long? ExpectedSizeBytes,
    UploadedFile UploadedFile
);
