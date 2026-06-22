using Bearcat.Abstractions;

namespace Bearcat.Infrastructure.FileSystem;

public class FileSystemService : IFileSystemService
{
    public List<string> GetFoldersInPath(string path)
    {
        return Directory
            .GetDirectories(
                path: path,
                searchPattern: "*",
                enumerationOptions: new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false,
                }
            )
            .ToList();
    }

    public List<string> GetFilesInPath(string path, bool recursive)
    {
        return Directory
            .GetFiles(
                path: path,
                searchPattern: "*",
                enumerationOptions: new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false,
                    RecurseSubdirectories = recursive,
                }
            )
            .ToList();
    }

    public string CreateTempDirectory(string basePath)
    {
        var folderPath = Path.Combine(basePath, Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(folderPath).FullName;
    }

    public bool FileExists(string filePath)
    {
        return File.Exists(filePath);
    }

    public void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }
}
