using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads;

public sealed class UploadExecutionContext(
    Upload upload,
    int totalFileCount,
    int successfulFileCount,
    int failedFileCount,
    long totalBytes,
    long alreadyUploadedBytes,
    CancellationTokenSource cancellationTokenSource
) : IDisposable
{
    public Upload Upload { get; } = upload;

    public int UploadId => Upload.Id;

    public int TotalFileCount { get; } = totalFileCount;

    public long TotalBytes { get; } = totalBytes;

    public long AlreadyUploadedBytes { get; } = alreadyUploadedBytes;

    public int SuccessfulFileCount { get; set; } = successfulFileCount;

    public int FailedFileCount { get; set; } = failedFileCount;

    public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

    public Queue<FileToUpload> PendingFiles { get; } = [];

    public int RunningFileCount { get; set; }

    public bool CancellationRequested { get; private set; }

    public int ProcessedFileCount => SuccessfulFileCount + FailedFileCount;

    public bool HasOpenWork => PendingFiles.Count > 0 || RunningFileCount > 0;

    public CancellationToken CancellationToken => CancellationTokenSource.Token;

    public void RequestCancellation()
    {
        CancellationRequested = true;
        Upload.UploadState = UploadState.CancellationRequested;
        PendingFiles.Clear();
        CancellationTokenSource.Cancel();
    }

    public void Dispose()
    {
        CancellationTokenSource.Dispose();
    }
}
