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
        var items = await client.GetListing(path, cancellationToken);

        return items
            .Where(item => item.Type == FtpObjectType.Directory)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new RemoteFolderDto(
                Name: item.Name,
                FullPath: item.FullName,
                ModifiedAt: item.Modified == DateTime.MinValue ? null : item.Modified
            ))
            .ToList();
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

                switch (item.Type)
                {
                    case FtpObjectType.File:
                        files.Add(new RemoteFileDto(relativePath, item.FullName, item.Size));
                        break;
                    case FtpObjectType.Directory:
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
