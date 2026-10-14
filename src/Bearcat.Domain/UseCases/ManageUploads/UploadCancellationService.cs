using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class UploadCancellationService(INotificationService notificationService)
{
    public void CancelUploadAndNotify(Upload upload, string notificationMessage)
    {
        upload.UploadState = UploadState.Canceled;
        upload.OnlineState = OnlineState.Unknown;

        notificationService.Create(
            kind: NotificationKind.UploadCanceled,
            message: notificationMessage,
            entity: upload,
            selector: n => n.Upload
        );
    }

    public void CancelUploadsAndNotify(IReadOnlyList<Upload> uploads, string notificationMessage)
    {
        foreach (var upload in uploads)
        {
            CancelUploadAndNotify(upload, notificationMessage);
        }
    }
}
