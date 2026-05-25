namespace Bearcat.Abstractions.Archiver;

public interface IArchiver
{
    string Name { get; }

    string FileExtension { get; }

    bool CanChangeHashInPlace { get; }

    Task<ArchiveResult> ArchiveAsync(
        string sourceFolderPath,
        string destinationPath,
        string archiveNamePrefix,
        int targetFileSizeMb,
        string? password,
        ArchiveOptions options,
        CancellationToken cancellationToken
    );
}
