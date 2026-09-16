namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

public sealed record PlannedDownloadFile(int ArchiveFileId, string FileName, long? SizeBytes);
