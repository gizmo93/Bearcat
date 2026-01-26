namespace Bearcat.Abstractions;

public interface IFileSystemService
{
    List<string> GetFoldersInPath(string path);
    string CreateTempDirectory(string basePath);
    bool FileExists(string filePath);
}
