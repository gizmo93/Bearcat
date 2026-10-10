namespace Bearcat.Domain.Shared.FolderConfirmation;

public static class FolderConfirmationMarkerFile
{
    public const string FileName = ".bearcat-folder";

    public static string GetFilePath(string rootPath)
    {
        return Path.Join(rootPath, FileName);
    }
}
