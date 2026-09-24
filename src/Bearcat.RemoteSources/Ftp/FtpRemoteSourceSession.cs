using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Abstractions.Transfers;
using FluentFTP;

namespace Bearcat.RemoteSources.Ftp;

public sealed class FtpRemoteSourceSession(AsyncFtpClient client) : IRemoteSourceSession
{
    private const string PartialFileExtension = ".part";

    public async Task<IReadOnlyList<RemoteFolderDto>> ListFoldersAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var folders = new List<RemoteFolderDto>();

        foreach (var item in await client.GetListing(path, cancellationToken))
        {
            var entry = await ResolveEntryAsync(item, cancellationToken);

            if (entry is { Type: FtpObjectType.Directory })
            {
                folders.Add(
                    new RemoteFolderDto(
                        Name: item.Name,
                        FullPath: item.FullName,
                        ModifiedAt: item.Modified == DateTime.MinValue ? null : item.Modified
                    )
                );
            }
        }

        return folders.OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<RemoteFileDto>> ListFilesRecursiveAsync(
        string folderPath,
        CancellationToken cancellationToken
    )
    {
        var files = new List<RemoteFileDto>();
        var pendingFolders = new Stack<(string FullPath, string RelativePath)>();
        pendingFolders.Push((folderPath, string.Empty));

        while (pendingFolders.TryPop(out var folder))
        {
            var items = await client.GetListing(folder.FullPath, cancellationToken);

            foreach (var item in items)
            {
                var relativePath =
                    folder.RelativePath.Length == 0
                        ? item.Name
                        : $"{folder.RelativePath}/{item.Name}";

                switch (await ResolveEntryAsync(item, cancellationToken))
                {
                    case { Type: FtpObjectType.File, Size: var size }:
                        files.Add(new RemoteFileDto(relativePath, item.FullName, size));
                        break;
                    case { Type: FtpObjectType.Directory }:
                        pendingFolders.Push((item.FullName, relativePath));
                        break;
                }
            }
        }

        return files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList();
    }

    public async Task DownloadFileAsync(
        RemoteFileDto file,
        string localFilePath,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        var partialFilePath = localFilePath + PartialFileExtension;
        Directory.CreateDirectory(Path.GetDirectoryName(localFilePath)!);
        progress.BeginFile(file.SizeBytes);

        try
        {
            var status = await client.DownloadFile(
                localPath: partialFilePath,
                remotePath: file.FullPath,
                existsMode: FtpLocalExists.Overwrite,
                verifyOptions: FtpVerify.None,
                progress: new DeltaProgress(progress),
                token: cancellationToken
            );

            if (status != FtpStatus.Success)
            {
                throw new IOException($"Download of {file.FullPath} failed with status {status}");
            }

            File.Move(partialFilePath, localFilePath, overwrite: true);
        }
        catch
        {
            File.Delete(partialFilePath);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        return client.DisposeAsync();
    }

    private async Task<ListingEntry?> ResolveEntryAsync(
        FtpListItem item,
        CancellationToken cancellationToken
    )
    {
        return item.Type switch
        {
            FtpObjectType.Directory => new ListingEntry(FtpObjectType.Directory, 0),
            FtpObjectType.File => new ListingEntry(FtpObjectType.File, item.Size),
            _ => await ResolveLinkAsync(item.FullName, cancellationToken),
        };
    }

    private async Task<ListingEntry?> ResolveLinkAsync(
        string linkPath,
        CancellationToken cancellationToken
    )
    {
        if (await client.DirectoryExists(linkPath, cancellationToken))
        {
            return new ListingEntry(FtpObjectType.Directory, 0);
        }

        var size = await client.GetFileSize(linkPath, -1, cancellationToken);

        return size < 0 ? null : new ListingEntry(FtpObjectType.File, size);
    }

    private sealed record ListingEntry(FtpObjectType Type, long Size);

    private sealed class DeltaProgress(ITransferProgress progress) : IProgress<FtpProgress>
    {
        private long reportedBytes;

        public void Report(FtpProgress value)
        {
            var delta = value.TransferredBytes - reportedBytes;
            if (delta <= 0)
            {
                return;
            }

            reportedBytes = value.TransferredBytes;
            progress.ReportBytesTransferred(delta);
        }
    }
}
