using Bearcat.Abstractions.Transfers;

namespace Bearcat.Abstractions;

public interface IFileSystemService
{
    List<string> GetFoldersInPath(string path);
    List<string> GetSelectableFoldersInPath(string path);
    List<string> GetFilesInPath(string path, bool recursive);
    FolderFileCountAndSize GetFolderFileCountAndSize(string path);
    string CreateTempDirectory(string basePath);
    bool FileExists(string filePath);
    bool DirectoryExists(string path);
    bool DirectoryHasEntries(string path);
    long? GetAvailableFreeSpaceBytes(string path);
    long GetFileSizeBytes(string filePath);
    void CreateDirectory(string path);
    void CopyFile(string sourceFilePath, string destinationFilePath);
    Task CopyFileAsync(
        string sourceFilePath,
        string destinationFilePath,
        ITransferProgress progress,
        CancellationToken cancellationToken
    );
    void CopyDirectoryRecursively(string sourceDirectoryPath, string destinationDirectoryPath);
    void DeleteFileIfExists(string filePath);
    void DeleteDirectoryIfExists(string path);
    void DeleteDirectoryIfEmpty(string path);
    IReadOnlyList<string> DeleteDirectoriesByNameRecursively(string rootPath, string directoryName);
}
