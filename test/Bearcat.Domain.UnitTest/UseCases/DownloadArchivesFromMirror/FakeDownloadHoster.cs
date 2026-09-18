using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Domain.UnitTest.UseCases.DownloadArchivesFromMirror;

public sealed class FakeDownloadHoster : IHosterWithDownload
{
    public List<string> DownloadedLinks { get; } = [];

    public Dictionary<string, string> ContentPerLink { get; } = new();

    public Dictionary<string, long?> ExpectedSizeBytesPerLink { get; } = new();

    public Dictionary<string, long> SizeBytesPerLink { get; } = new();

    public Exception? FileSizeLookupException { get; set; }

    public bool BlockUntilCanceled { get; set; }

    public bool ReportCancellationAsFailure { get; set; }

    public TaskCompletionSource DownloadStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<IReadOnlyDictionary<string, long>> GetFileSizesAsync(
        IReadOnlyList<string> fileUrls,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        if (FileSizeLookupException is not null)
        {
            throw FileSizeLookupException;
        }

        IReadOnlyDictionary<string, long> sizes = fileUrls
            .Where(SizeBytesPerLink.ContainsKey)
            .ToDictionary(fileUrl => fileUrl, fileUrl => SizeBytesPerLink[fileUrl]);

        return Task.FromResult(sizes);
    }

    public string Name => "FakeDownloadHoster";

    public bool SupportsPremiumOnlyDownloads => false;

    public bool DownloadRequiresPremium => false;

    public bool HasFixedParallelUploadLimit => false;

    public int? DefaultMaximumParallelUploads => 1;

    public IReadOnlyList<string> ConfigurationKeys => [];

    public async Task<DownloadFileResult> DownloadFileAsync(
        DownloadFileDto file,
        string targetFilePath,
        IHosterConfig hosterConfig,
        IDownloadProgress progress,
        CancellationToken cancellationToken
    )
    {
        lock (DownloadedLinks)
        {
            DownloadedLinks.Add(file.HosterFileLink);
            ExpectedSizeBytesPerLink[file.HosterFileLink] = file.ExpectedSizeBytes;
        }

        DownloadStarted.TrySetResult();

        if (BlockUntilCanceled)
        {
            await WaitForCancellationAsync(cancellationToken);

            if (ReportCancellationAsFailure)
            {
                return new DownloadFileResult(
                    IsSuccess: false,
                    ErrorMessages: ["The connection was closed"]
                );
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        var content = ContentPerLink.GetValueOrDefault(file.HosterFileLink, "payload");

        progress.BeginFile(content.Length);

        await File.WriteAllTextAsync(targetFilePath, content, cancellationToken);
        progress.ReportBytesTransferred(content.Length);

        return new DownloadFileResult(IsSuccess: true, ErrorMessages: []);
    }

    private static async Task WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        await using var registration = cancellationToken.Register(() => completion.TrySetResult());

        await completion.Task;
    }

    public IHosterConfig DeserializeHosterConfig(string serializedConfig) =>
        new FakeDownloadHosterConfig();

    public string SerializeHosterConfig(Dictionary<string, string> hosterConfig) => string.Empty;

    public Task<UploadFileResult> UploadFileAsync(
        FileDto fileDto,
        IHosterConfig hosterConfig,
        IUploadProgress progress,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    public Task<FileExistResult> CheckFilesExistAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    public Task<int?> GetMaximumParallelUploadsAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    public Task<TryLoginResult> TryLoginAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();
}
