using Bearcat.Abstractions;

namespace Bearcat.Domain.UnitTest.Shared.QualityGate;

public sealed class FakeFileSystemService : IFileSystemService
{
    public bool DirectoryExistsResult { get; init; } = true;

    public List<string> Files { get; init; } = [];

    public long TotalBytes { get; init; }

    public List<string> GetFilesInPath(string path, bool recursive) => Files;

    public bool DirectoryExists(string path) => DirectoryExistsResult;

    public FolderContentFingerprint GetFolderContentFingerprint(string path) =>
        new(Files.Count, TotalBytes);

    public List<string> GetFoldersInPath(string path) => throw new NotSupportedException();

    public string CreateTempDirectory(string basePath) => throw new NotSupportedException();

    public bool FileExists(string filePath) => throw new NotSupportedException();

    public void DeleteDirectoryIfExists(string path) => throw new NotSupportedException();

    public IReadOnlyList<string> DeleteDirectoriesByNameRecursively(
        string rootPath,
        string directoryName
    ) => throw new NotSupportedException();
}
