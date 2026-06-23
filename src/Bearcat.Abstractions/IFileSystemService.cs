namespace Bearcat.Abstractions;

public interface IFileSystemService
{
    List<string> GetFoldersInPath(string path);
    List<string> GetFilesInPath(string path, bool recursive);
    FolderContentFingerprint GetFolderContentFingerprint(string path);
    string CreateTempDirectory(string basePath);
    bool FileExists(string filePath);
    void DeleteDirectoryIfExists(string path);
    IReadOnlyList<string> DeleteDirectoriesByNameRecursively(string rootPath, string directoryName);
}

public readonly record struct FolderContentFingerprint(int FileCount, long TotalBytes);
