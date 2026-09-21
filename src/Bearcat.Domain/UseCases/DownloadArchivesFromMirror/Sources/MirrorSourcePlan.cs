using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;

public sealed record MirrorSourcePlan(
    IReadOnlyDictionary<int, IReadOnlyList<MirrorSource>> SourcesPerArchiveFileId,
    IReadOnlyList<ArchiveFile> FilesWithoutSource
);
