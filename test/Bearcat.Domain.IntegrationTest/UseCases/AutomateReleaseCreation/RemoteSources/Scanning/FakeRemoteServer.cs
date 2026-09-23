using Bearcat.Abstractions.RemoteSource.Dto;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

public sealed class FakeRemoteServer
{
    private readonly Dictionary<string, Dictionary<string, List<long>>> foldersByParentPath = new(
        StringComparer.Ordinal
    );

    public Exception? OpenException { get; set; }

    public int OpenCount { get; private set; }

    public int DisposeCount { get; private set; }

    public List<string> RecursivelyListedPaths { get; } = [];

    public List<string> ListedPaths { get; } = [];

    public void AddParent(string parentPath)
    {
        foldersByParentPath.TryAdd(parentPath, new Dictionary<string, List<long>>());
    }

    public void SetFolder(string parentPath, string folderName, params long[] fileSizes)
    {
        AddParent(parentPath);
        foldersByParentPath[parentPath][folderName] = [.. fileSizes];
    }

    public void RemoveFolder(string parentPath, string folderName)
    {
        foldersByParentPath[parentPath].Remove(folderName);
    }

    public static string CombinePath(string parentPath, string folderName)
    {
        return parentPath == "/" ? $"/{folderName}" : $"{parentPath}/{folderName}";
    }

    public void MarkOpened()
    {
        OpenCount++;
    }

    public void MarkDisposed()
    {
        DisposeCount++;
    }

    public IReadOnlyList<RemoteFolderDto> ListFolders(string path)
    {
        ListedPaths.Add(path);

        if (!foldersByParentPath.TryGetValue(path, out var folders))
        {
            throw new DirectoryNotFoundException($"Remote path {path} does not exist.");
        }

        return folders
            .Keys.Order(StringComparer.Ordinal)
            .Select(name => new RemoteFolderDto(name, CombinePath(path, name), ModifiedAt: null))
            .ToList();
    }

    public IReadOnlyList<RemoteFileDto> ListFilesRecursive(string folderPath)
    {
        RecursivelyListedPaths.Add(folderPath);

        var separatorIndex = folderPath.LastIndexOf('/');
        var parentPath = separatorIndex == 0 ? "/" : folderPath[..separatorIndex];
        var folderName = folderPath[(separatorIndex + 1)..];

        return foldersByParentPath[parentPath]
            [folderName]
            .Select(
                (size, index) =>
                    new RemoteFileDto($"file{index}.rar", $"{folderPath}/file{index}.rar", size)
            )
            .ToList();
    }
}
