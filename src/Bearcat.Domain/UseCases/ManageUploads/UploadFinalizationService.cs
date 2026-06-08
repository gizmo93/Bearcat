using Bearcat.Domain.Shared;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class UploadFinalizationService(
    TimeProvider timeProvider,
    ILogger<UploadFinalizationService> logger,
    INotificationService notificationService
)
{
    public bool TryFinalizeUpload(UploadExecutionContext context)
    {
        if (context.SuccessfulFileCount == context.TotalFileCount)
        {
            CompleteUpload(context);
            return true;
        }

        if (context is { CancellationRequested: true, HasOpenWork: false })
        {
            CancelUpload(context);
            return true;
        }

        if (context.ProcessedFileCount != context.TotalFileCount)
        {
            return false;
        }

        FailUpload(context);
        return true;
    }

    public void CompleteUpload(UploadExecutionContext context)
    {
        context.Upload.UploadState = UploadState.Completed;
        context.Upload.OnlineState = OnlineState.Online;
        context.Upload.UploadedAt = timeProvider.GetLocalNow();

        notificationService.CreateInfo(
            message: "All files uploaded successfully",
            entity: context.Upload,
            selector: n => n.Upload
        );

        logger.LogInformation(
            "Completed upload for Upload {UploadId} to hoster {Hoster} with state {UploadState}",
            context.UploadId,
            context.Upload.UploadConfig.HosterRegistration.HosterClassName,
            context.Upload.UploadState
        );

        context.Dispose();
    }

    public void FailUpload(UploadExecutionContext context)
    {
        context.Upload.UploadState = UploadState.Failed;
        context.Upload.OnlineState = OnlineState.PartiallyOnline;

        notificationService.CreateError(
            message: "Some files failed to upload",
            entity: context.Upload,
            selector: n => n.Upload
        );

        logger.LogInformation(
            "Completed upload for Upload {UploadId} to hoster {Hoster} with state {UploadState}",
            context.UploadId,
            context.Upload.UploadConfig.HosterRegistration.HosterClassName,
            context.Upload.UploadState
        );

        context.Dispose();
    }

    public void CancelUpload(UploadExecutionContext context)
    {
        context.Upload.UploadState = UploadState.Canceled;
        context.Upload.OnlineState = OnlineState.Unknown;

        notificationService.CreateInfo(
            message: "Upload canceled",
            entity: context.Upload,
            selector: n => n.Upload
        );

        logger.LogInformation(
            "Canceled upload for Upload {UploadId} to hoster {Hoster}",
            context.UploadId,
            context.Upload.UploadConfig.HosterRegistration.HosterClassName
        );

        context.Dispose();
    }
}
