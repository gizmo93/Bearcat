namespace Bearcat.Abstractions.Archiver;

public interface IArchiveExtractor
{
    IReadOnlyList<ArchiveToExtract> FindArchivesToExtract(string folderPath);

    Task<ArchiveExtractionResult> ExtractAsync(
        ArchiveToExtract archiveToExtract,
        string destinationFolderPath,
        CancellationToken cancellationToken
    );
}
