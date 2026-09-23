namespace Bearcat.Abstractions;

public interface IFileSystemService
{
    List<string> GetFoldersInPath(string path);
    List<string> GetFilesInPath(string path, bool recursive);
    FolderContentFingerprint GetFolderContentFingerprint(string path);
    string CreateTempDirectory(string basePath);
    bool FileExists(string filePath);
    bool DirectoryExists(string path);
    bool DirectoryHasEntries(string path);
    void DeleteFileIfExists(string filePath);
    void DeleteDirectoryIfExists(string path);
    void DeleteDirectoryIfEmpty(string path);
    IReadOnlyList<string> DeleteDirectoriesByNameRecursively(string rootPath, string directoryName);
}

public readonly record struct FolderContentFingerprint(int FileCount, long TotalBytes);
