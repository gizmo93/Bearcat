namespace Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;

public sealed class ArchiveOutputFolderInsideSourceFolderException(
    string archiveOutputFolderPath,
    string sourceFolderPath
)
    : Exception(
        $"The archive output folder '{archiveOutputFolderPath}' is inside the source folder '{sourceFolderPath}'."
    )
{
    public string ArchiveOutputFolderPath { get; } = archiveOutputFolderPath;

    public string SourceFolderPath { get; } = sourceFolderPath;
}
