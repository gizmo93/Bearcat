using System.Threading.Channels;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution;

public sealed class UploadsInProgress(ITransferProgressTracker progressTracker, ILogger logger)
{
    private readonly Dictionary<int, UploadExecutionContext> contextsByUploadId = new();

    public IReadOnlyDictionary<int, UploadExecutionContext> ContextsByUploadId =>
        contextsByUploadId;

    public HashSet<int> PickedUpUploadIds { get; } = [];

    public List<Task> RunningFileUploadTasks { get; } = [];

    public Channel<FileUploadCompleted> CompletedFileUploads { get; } =
        Channel.CreateUnbounded<FileUploadCompleted>(
            options: new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );

    public bool HasUploadsOrRunningFileUploads =>
        contextsByUploadId.Count > 0 || RunningFileUploadTasks.Count > 0;

    public void StartUploading(UploadExecutionContext context)
    {
        context.Upload.UploadState = UploadState.Uploading;
        contextsByUploadId[context.UploadId] = context;
        progressTracker.StartTracking(
            new TransferIdentifier(TransferType.Upload, context.UploadId),
            context.PlannedFiles
        );
    }

    public void RemoveFinishedUpload(int uploadId)
    {
        contextsByUploadId.Remove(uploadId);
        progressTracker.StopTracking(new TransferIdentifier(TransferType.Upload, uploadId));
    }

    public async Task RemoveCompletedFileUploadTasksAndLogUnexpectedErrorsAsync(
        CancellationToken cancellationToken
    )
    {
        var completedTasks = RunningFileUploadTasks.Where(t => t.IsCompleted).ToList();

        foreach (var task in completedTasks)
        {
            RunningFileUploadTasks.Remove(task);

            try
            {
                await task;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in file upload worker");
            }
        }
    }

    public Dictionary<int, long> GetRemainingBytesOfRunningUploadsByHosterRegistrationId()
    {
        return contextsByUploadId
            .Values.GroupBy(context => context.Upload.HosterRegistrationId!.Value)
            .ToDictionary(
                contextsOfRegistration => contextsOfRegistration.Key,
                contextsOfRegistration =>
                    contextsOfRegistration.Sum(context => context.GetBytesOfFilesNotYetUploaded())
            );
    }
}
