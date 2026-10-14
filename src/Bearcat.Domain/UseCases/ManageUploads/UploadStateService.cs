using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class UploadStateService(
    IUploadStateRepository uploadStateRepository,
    TimeProvider timeProvider,
    INotificationService notificationService,
    ILogger<UploadStateService> logger
)
{
    public async Task<bool> CancelUploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetByIdAsync(uploadId, cancellationToken);

        if (upload is null)
        {
            return false;
        }

        if (upload.UploadState == UploadState.CancellationRequested)
        {
            return true;
        }

        if (upload.UploadState is not UploadState.Pending and not UploadState.Uploading)
        {
            return false;
        }

        upload.UploadState = UploadState.CancellationRequested;

        notificationService.Create(
            kind: NotificationKind.UploadCancellationRequested,
            message: "Upload cancellation requested",
            entity: upload,
            selector: u => u.Upload
        );

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ResumeUploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetByIdAsync(uploadId, cancellationToken);

        if (upload is null)
        {
            return false;
        }

        if (upload.UploadState != UploadState.Canceled)
        {
            return false;
        }

        upload.UploadState = upload.ArchiveId is null
            ? UploadState.WaitingForArchive
            : UploadState.Pending;

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> SetUploadOfflineAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetUploadForOnlineCheckAsync(
            uploadId: uploadId,
            cancellationToken: cancellationToken
        );

        if (upload is null)
        {
            return false;
        }

        if (upload.OnlineState is not OnlineState.Online and not OnlineState.PartiallyOnline)
        {
            return false;
        }

        var localNow = timeProvider.GetLocalNow();

        foreach (var file in upload.UploadedFiles)
        {
            file.SetOnlineStateAndCheckedAt(OnlineState.Offline, localNow);
        }

        upload.SetOnlineStateAndOfflineSinceTimestamps(OnlineState.Offline, localNow);

        notificationService.Create(
            kind: NotificationKind.UploadMarkedOffline,
            message: "Upload manually marked as offline",
            entity: upload,
            selector: u => u.Upload
        );

        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Upload {UploadId} was manually marked as offline", upload.Id);

        return true;
    }

    public async Task<bool> DeleteUploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadStateRepository.GetByIdAsync(uploadId, cancellationToken);

        if (upload is null)
        {
            return false;
        }

        if (!upload.CanBeDeleted())
        {
            return false;
        }

        uploadStateRepository.Remove(upload);
        await uploadStateRepository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
