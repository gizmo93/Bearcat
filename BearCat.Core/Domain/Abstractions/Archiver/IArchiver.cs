namespace BearCat.Core.Domain.Abstractions.Archiver;

public interface IArchiver
{
    string Name { get; }

    Task<ArchiveResult> ArchiveAsync(
        string sourceFolderPath,
        string destinationPath,
        string archiveNamePrefix,
        int targetFileSizeMb,
        string? password,
        CancellationToken cancellationToken);
}
