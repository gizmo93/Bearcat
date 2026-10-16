namespace Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;

public sealed class ArchiveOutputFolderNotInsideExistingWorkingDirectoryException(string folderPath)
    : Exception(
        $"The archive output folder '{folderPath}' is not inside an existing working directory."
    )
{
    public string FolderPath { get; } = folderPath;
}
