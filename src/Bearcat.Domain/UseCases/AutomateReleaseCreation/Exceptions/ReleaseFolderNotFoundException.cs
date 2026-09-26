namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.Exceptions;

public sealed class ReleaseFolderNotFoundException(string folderPath)
    : Exception($"The folder '{folderPath}' does not exist.")
{
    public string FolderPath { get; } = folderPath;
}
