using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Abstractions.Transfers;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources;

public sealed class FakeRemoteServer
{
    private readonly Lock gate = new();

    private readonly Dictionary<
        string,
        Dictionary<string, List<FakeRemoteFile>>
    > foldersByParentPath = new(StringComparer.Ordinal);

    private readonly Dictionary<string, int> remainingFailuresByRelativePath = new(
        StringComparer.Ordinal
    );

    private readonly HashSet<string> blockedRelativePaths = new(StringComparer.Ordinal);

    private int openSessionCount;

    private int runningDownloadCount;

    public Exception? OpenException { get; set; }

    public TimeSpan DownloadDelay { get; set; }

    public int OpenCount { get; private set; }

    public int DisposeCount { get; private set; }

    public int MaxConcurrentSessions { get; private set; }

    public int MaxConcurrentDownloads { get; private set; }

    public List<string> RecursivelyListedPaths { get; } = [];

    public List<string> ListedPaths { get; } = [];

    public List<string> DownloadAttempts { get; } = [];

    public TaskCompletionSource BlockedDownloadStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void AddParent(string parentPath)
    {
        foldersByParentPath.TryAdd(parentPath, new Dictionary<string, List<FakeRemoteFile>>());
    }

    public void SetFolder(string parentPath, string folderName, params long[] fileSizes)
    {
        SetFolderFiles(
            parentPath,
            folderName,
            [.. fileSizes.Select((size, index) => new FakeRemoteFile($"file{index}.rar", size))]
        );
    }

    public void SetFolderFiles(string parentPath, string folderName, params FakeRemoteFile[] files)
    {
        AddParent(parentPath);
        foldersByParentPath[parentPath][folderName] = [.. files];
    }

    public void RemoveFolder(string parentPath, string folderName)
    {
        foldersByParentPath[parentPath].Remove(folderName);
    }

    public void FailDownload(string relativePath, int failureCount = int.MaxValue)
    {
        remainingFailuresByRelativePath[relativePath] = failureCount;
    }

    public void BlockDownload(string relativePath)
    {
        blockedRelativePaths.Add(relativePath);
    }

    public static string CombinePath(string parentPath, string folderName)
    {
        return parentPath == "/" ? $"/{folderName}" : $"{parentPath}/{folderName}";
    }

    public void MarkOpened()
    {
        lock (gate)
        {
            OpenCount++;
            openSessionCount++;
            MaxConcurrentSessions = Math.Max(MaxConcurrentSessions, openSessionCount);
        }
    }

    public void MarkDisposed()
    {
        lock (gate)
        {
            DisposeCount++;
            openSessionCount--;
        }
    }

    public IReadOnlyList<RemoteFolderDto> ListFolders(string path)
    {
        lock (gate)
        {
            ListedPaths.Add(path);
        }

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
        lock (gate)
        {
            RecursivelyListedPaths.Add(folderPath);
        }

        var separatorIndex = folderPath.LastIndexOf('/');
        var parentPath = separatorIndex == 0 ? "/" : folderPath[..separatorIndex];
        var folderName = folderPath[(separatorIndex + 1)..];

        return foldersByParentPath[parentPath]
            [folderName]
            .Select(file => new RemoteFileDto(
                file.RelativePath,
                $"{folderPath}/{file.RelativePath}",
                file.SizeBytes
            ))
            .ToList();
    }

    public async Task DownloadAsync(
        RemoteFileDto file,
        string localFilePath,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        lock (gate)
        {
            DownloadAttempts.Add(file.RelativePath);
            runningDownloadCount++;
            MaxConcurrentDownloads = Math.Max(MaxConcurrentDownloads, runningDownloadCount);
        }

        var partialFilePath = localFilePath + ".part";

        try
        {
            progress.BeginFile(file.SizeBytes);

            if (blockedRelativePaths.Contains(file.RelativePath))
            {
                BlockedDownloadStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (DownloadDelay > TimeSpan.Zero)
            {
                await Task.Delay(DownloadDelay, cancellationToken);
            }

            ThrowIfFailureIsConfigured(file.RelativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(localFilePath)!);
            await File.WriteAllBytesAsync(
                partialFilePath,
                new byte[file.SizeBytes],
                cancellationToken
            );
            File.Move(partialFilePath, localFilePath, overwrite: true);
            progress.ReportBytesTransferred(file.SizeBytes);
        }
        catch
        {
            File.Delete(partialFilePath);
            throw;
        }
        finally
        {
            lock (gate)
            {
                runningDownloadCount--;
            }
        }
    }

    private void ThrowIfFailureIsConfigured(string relativePath)
    {
        lock (gate)
        {
            if (
                !remainingFailuresByRelativePath.TryGetValue(relativePath, out var remaining)
                || remaining == 0
            )
            {
                return;
            }

            remainingFailuresByRelativePath[relativePath] = remaining - 1;
        }

        throw new IOException($"Transfer of {relativePath} was interrupted");
    }
}
