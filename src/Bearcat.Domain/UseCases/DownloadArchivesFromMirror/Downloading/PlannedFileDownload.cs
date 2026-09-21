using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public sealed record PlannedFileDownload(
    int ArchiveFileId,
    string TargetFilePath,
    long? ExpectedSizeBytes,
    IReadOnlyList<MirrorSource> Sources
);
