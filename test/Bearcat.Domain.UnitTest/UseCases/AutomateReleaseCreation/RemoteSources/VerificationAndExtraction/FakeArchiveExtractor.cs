using Bearcat.Abstractions.Archiver;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;

public sealed class FakeArchiveExtractor : IArchiveExtractor
{
    private readonly List<ArchiveToExtract> archives = [];
    private readonly Dictionary<string, Dictionary<string, string>> extractedFilesByFirstVolume =
        new(StringComparer.Ordinal);

    public List<string> DestinationFolderPaths { get; } = [];

    public ArchiveExtractionResult? FailureResult { get; set; }

    public Exception? ExceptionWhenSearching { get; set; }

    public void AddArchive(
        ArchiveToExtract archiveToExtract,
        Dictionary<string, string> extractedFilesByRelativePath
    )
    {
        archives.Add(archiveToExtract);
        extractedFilesByFirstVolume[archiveToExtract.FirstVolumeFilePath] =
            extractedFilesByRelativePath;
    }

    public IReadOnlyList<ArchiveToExtract> FindArchivesToExtract(string folderPath)
    {
        if (ExceptionWhenSearching is not null)
        {
            throw ExceptionWhenSearching;
        }

        return archives
            .Where(archive => Path.GetDirectoryName(archive.FirstVolumeFilePath) == folderPath)
            .ToList();
    }

    public async Task<ArchiveExtractionResult> ExtractAsync(
        ArchiveToExtract archiveToExtract,
        string destinationFolderPath,
        CancellationToken cancellationToken
    )
    {
        DestinationFolderPaths.Add(destinationFolderPath);

        foreach (
            var (relativePath, content) in extractedFilesByFirstVolume[
                archiveToExtract.FirstVolumeFilePath
            ]
        )
        {
            var filePath = Path.Combine(destinationFolderPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(filePath, content, cancellationToken);
        }

        return FailureResult ?? new ArchiveExtractionResult(true, []);
    }
}
