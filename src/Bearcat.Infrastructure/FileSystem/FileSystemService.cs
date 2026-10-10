using Bearcat.Abstractions;
using Bearcat.Abstractions.Transfers;

namespace Bearcat.Infrastructure.FileSystem;

public class FileSystemService : IFileSystemService
{
    private const int CopyBufferSizeBytes = 1024 * 1024;

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

    public List<string> GetSelectableFoldersInPath(string path)
    {
        return Directory
            .GetDirectories(
                path: path,
                searchPattern: "*",
                enumerationOptions: new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false,
                    AttributesToSkip = FileAttributes.System,
                }
            )
            .Where(folderPath => !Path.GetFileName(folderPath).StartsWith('.'))
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

    public FolderFileCountAndSize GetFolderFileCountAndSize(string path)
    {
        if (!Directory.Exists(path))
        {
            return new FolderFileCountAndSize(0, 0);
        }

        var fileCount = 0;
        long totalBytes = 0;

        foreach (
            var fileInfo in new DirectoryInfo(path).EnumerateFiles(
                searchPattern: "*",
                enumerationOptions: new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false,
                    RecurseSubdirectories = true,
                }
            )
        )
        {
            fileCount++;
            totalBytes += fileInfo.Length;
        }

        return new FolderFileCountAndSize(fileCount, totalBytes);
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

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public bool DirectoryHasEntries(string path)
    {
        return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
    }

    public long? GetAvailableFreeSpaceBytes(string path)
    {
        if (!Directory.Exists(path))
        {
            return null;
        }

        if (OperatingSystem.IsWindows())
        {
            return WindowsDiskFreeSpace.GetAvailableFreeSpaceBytes(path);
        }

        return new DriveInfo(path).AvailableFreeSpace;
    }

    public long GetFileSizeBytes(string filePath)
    {
        return new FileInfo(filePath).Length;
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void CopyFile(string sourceFilePath, string destinationFilePath)
    {
        File.Copy(sourceFilePath, destinationFilePath, overwrite: false);
    }

    public async Task CopyFileAsync(
        string sourceFilePath,
        string destinationFilePath,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        await using var sourceStream = new FileStream(
            path: sourceFilePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: CopyBufferSizeBytes,
            useAsync: true
        );
        await using var progressStream = new ProgressReportingStream(
            inner: sourceStream,
            progress: progress,
            totalBytes: sourceStream.Length
        );
        var destinationStream = new FileStream(
            path: destinationFilePath,
            mode: FileMode.CreateNew,
            access: FileAccess.Write,
            share: FileShare.None,
            bufferSize: CopyBufferSizeBytes,
            useAsync: true
        );

        try
        {
            await using (destinationStream)
            {
                await progressStream.CopyToAsync(
                    destinationStream,
                    CopyBufferSizeBytes,
                    cancellationToken
                );
                await destinationStream.FlushAsync(cancellationToken);
            }
        }
        catch
        {
            File.Delete(destinationFilePath);
            throw;
        }
    }

    public void CopyDirectoryRecursively(
        string sourceDirectoryPath,
        string destinationDirectoryPath
    )
    {
        Directory.CreateDirectory(destinationDirectoryPath);

        foreach (var sourceFilePath in Directory.GetFiles(sourceDirectoryPath))
        {
            File.Copy(
                sourceFilePath,
                Path.Join(destinationDirectoryPath, Path.GetFileName(sourceFilePath)),
                overwrite: false
            );
        }

        foreach (var sourceSubdirectoryPath in Directory.GetDirectories(sourceDirectoryPath))
        {
            CopyDirectoryRecursively(
                sourceSubdirectoryPath,
                Path.Join(destinationDirectoryPath, Path.GetFileName(sourceSubdirectoryPath))
            );
        }
    }

    public void DeleteFileIfExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        File.Delete(filePath);
    }

    public void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }

    public void DeleteDirectoryIfEmpty(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(path).Any())
        {
            return;
        }

        Directory.Delete(path, recursive: false);
    }

    public IReadOnlyList<string> DeleteDirectoriesByNameRecursively(
        string rootPath,
        string directoryName
    )
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        var matchingDirectories = Directory.GetDirectories(
            path: rootPath,
            searchPattern: directoryName,
            enumerationOptions: new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.None,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                RecurseSubdirectories = true,
            }
        );

        var deletedDirectories = new List<string>();

        foreach (var directory in matchingDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            Directory.Delete(directory, recursive: true);
            deletedDirectories.Add(directory);
        }

        return deletedDirectories;
    }
}
