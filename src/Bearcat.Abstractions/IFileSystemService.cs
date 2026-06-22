namespace Bearcat.Abstractions;

public interface IFileSystemService
{
    List<string> GetFoldersInPath(string path);
    List<string> GetFilesInPath(string path, bool recursive);
    string CreateTempDirectory(string basePath);
    bool FileExists(string filePath);
    void DeleteDirectoryIfExists(string path);
}
