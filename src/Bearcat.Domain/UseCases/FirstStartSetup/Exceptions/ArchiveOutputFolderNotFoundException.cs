namespace Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;

public sealed class ArchiveOutputFolderNotFoundException(string folderPath)
    : Exception($"The archive output folder '{folderPath}' does not exist.")
{
    public string FolderPath { get; } = folderPath;
}
