using Bearcat.Domain.UseCases.ManageUploads.Repositories;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution;

public class UploadCancellationRequestService(
    IUploadFilesRepository repository,
    UploadFinalizationService finalizationService,
    UploadCancellationService uploadCancellationService
)
{
    public async Task ApplyCancellationRequestsToUploadsInProgressAsync(
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        var cancellationRequestedUploadIds =
            await repository.GetCancellationRequestedUploadIdsAsync(cancellationToken);

        var hasChanges = false;

        foreach (var uploadId in cancellationRequestedUploadIds)
        {
            if (uploadsInProgress.ContextsByUploadId.TryGetValue(uploadId, out var context))
            {
                if (!context.CancellationRequested)
                {
                    context.RequestCancellation();
                    hasChanges = true;
                }

                if (finalizationService.TryFinalizeUpload(context))
                {
                    uploadsInProgress.RemoveFinishedUpload(uploadId);
                    hasChanges = true;
                }

                continue;
            }

            if (await CancelUploadWithoutRunningContextAsync(uploadId, cancellationToken))
            {
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CancelUploadsWithUnprocessedCancellationRequestAsync(
        CancellationToken cancellationToken
    )
    {
        var uploadIds = await repository.GetCancellationRequestedUploadIdsAsync(cancellationToken);

        foreach (var uploadId in uploadIds)
        {
            await CancelUploadWithoutRunningContextAsync(uploadId, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        repository.ClearChangeTracker();
    }

    private async Task<bool> CancelUploadWithoutRunningContextAsync(
        int uploadId,
        CancellationToken cancellationToken
    )
    {
        var upload = await repository.GetUploadByIdAsync(uploadId, cancellationToken);

        if (upload is null)
        {
            return false;
        }

        uploadCancellationService.CancelUploadAndNotify(upload, "Upload canceled");

        return true;
    }
}
