using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution;

public sealed class UploadExecutionContext(
    Upload upload,
    IReadOnlyList<UploadSourceFile> sourceFiles,
    int successfulFileCount,
    int failedFileCount,
    IReadOnlyList<TransferFile> plannedFiles,
    CancellationTokenSource cancellationTokenSource
) : IDisposable
{
    public Upload Upload { get; } = upload;

    public int UploadId => Upload.Id;

    public IReadOnlyList<UploadSourceFile> SourceFiles { get; } = sourceFiles;

    public int TotalFileCount => SourceFiles.Count;

    public IReadOnlyList<TransferFile> PlannedFiles { get; } = plannedFiles;

    public int SuccessfulFileCount { get; set; } = successfulFileCount;

    public int FailedFileCount { get; set; } = failedFileCount;

    public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

    public Queue<FileToUpload> PendingFiles { get; } = [];

    public int RunningFileCount { get; set; }

    public bool CancellationRequested { get; private set; }

    public int ProcessedFileCount => SuccessfulFileCount + FailedFileCount;

    public bool HasOpenWork => PendingFiles.Count > 0 || RunningFileCount > 0;

    public CancellationToken CancellationToken => CancellationTokenSource.Token;

    public long GetBytesOfFilesNotYetUploaded()
    {
        var transferFileIdsWithoutUploadedFile = SourceFiles
            .Where(sourceFile => !Upload.UploadedFiles.Any(sourceFile.IsSourceOf))
            .Select(sourceFile => sourceFile.TransferFileId)
            .ToHashSet();

        return PlannedFiles
            .Where(file => transferFileIdsWithoutUploadedFile.Contains(file.FileId))
            .Sum(file => file.SizeBytes!.Value);
    }

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
