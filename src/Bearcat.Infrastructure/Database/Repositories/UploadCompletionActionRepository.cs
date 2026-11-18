using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.ReadModels;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadCompletionActionRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IUploadCompletionActionRepository, IUploadCompletionActionReleaseStateReadRepository
{
    public async Task<
        IReadOnlyList<UploadCompletionActionRegistration>
    > GetActiveRegistrationsWithReadableSecretsAsync(CancellationToken cancellationToken = default)
    {
        return await dbRead
            .UploadCompletionActionRegistrations.Where(registration =>
                registration.IsActive && !registration.HasUnreadableSecrets
            )
            .OrderBy(registration => registration.Name)
            .ThenBy(registration => registration.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<UploadCompletionActionRegistration> GetRegistrationAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.UploadCompletionActionRegistrations.SingleAsync(
            registration => registration.Id == id,
            cancellationToken
        );
    }

    public async Task<UploadCompletionActionDueRelease?> GetCompletelyUploadedReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var uploadIsReupload = UploadExpressions.UploadIsReupload(dbRead);
        var completedUploads = GetCompletedUploads();

        return await dbRead
            .Releases.Where(release => release.Id == releaseId)
            .Where(ReleaseReadRepository.AllUploadsLinkCrypterContainersAndImagesAreComplete)
            .Select(release => new
            {
                NewestUploadedAt = completedUploads
                    .Where(upload => upload.UploadConfig.ReleaseId == release.Id)
                    .Max(upload => upload.UploadedAt),
                IsReupload = completedUploads
                    .Where(upload => upload.UploadConfig.ReleaseId == release.Id)
                    .Any(uploadIsReupload),
            })
            .Where(release => release.NewestUploadedAt != null)
            .Select(release => new UploadCompletionActionDueRelease(
                ReleaseId: releaseId,
                IsReupload: release.IsReupload,
                NewestUploadedAt: release.NewestUploadedAt!.Value
            ))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<
        IReadOnlyList<UploadCompletionActionReleaseStateReadModel>
    > GetReleaseStatesAsync(int releaseId, CancellationToken cancellationToken = default)
    {
        var completedUploads = GetCompletedUploads();

        var completelyUploadedReleases = dbRead
            .Releases.Where(release => release.Id == releaseId)
            .Where(ReleaseReadRepository.AllUploadsLinkCrypterContainersAndImagesAreComplete)
            .Where(release =>
                completedUploads.Any(upload => upload.UploadConfig.ReleaseId == release.Id)
            );

        var executionsOfRelease = dbRead.UploadCompletionActionExecutions.Where(execution =>
            execution.ReleaseId == releaseId
        );

        return await dbRead
            .UploadCompletionActionRegistrations.Where(registration =>
                registration.IsActive && !registration.HasUnreadableSecrets
            )
            .LeftJoin(
                executionsOfRelease,
                registration => registration.Id,
                execution => execution.UploadCompletionActionRegistrationId,
                (registration, execution) =>
                    new
                    {
                        RegistrationId = registration.Id,
                        RegistrationName = registration.Name,
                        ExecutedAt = execution == null ? (DateTime?)null : execution.ExecutedAt,
                        ResultText = execution == null ? null : execution.ResultText,
                        ErrorMessage = execution == null ? null : execution.ErrorMessage,
                    }
            )
            .OrderBy(state => state.RegistrationName)
            .ThenBy(state => state.RegistrationId)
            .Select(state => new UploadCompletionActionReleaseStateReadModel(
                RegistrationId: state.RegistrationId,
                RegistrationName: state.RegistrationName,
                IsReleaseCompletelyUploaded: completelyUploadedReleases.Any(),
                ExecutedAt: state.ExecutedAt,
                ResultText: state.ResultText,
                ErrorMessage: state.ErrorMessage
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UploadCompletionActionDueRelease>> GetDueReleasesAsync(
        UploadCompletionActionRegistration registration,
        CancellationToken cancellationToken = default
    )
    {
        var registrationCreatedAt = registration.CreatedAt;
        var runForInitialUpload = registration.RunForInitialUpload;
        var runForReupload = registration.RunForReupload;
        var uploadIsReupload = UploadExpressions.UploadIsReupload(dbRead);

        var executionsOfRegistration = dbRead.UploadCompletionActionExecutions.Where(execution =>
            execution.UploadCompletionActionRegistrationId == registration.Id
        );

        var completedUploads = GetCompletedUploads();

        return await dbRead
            .Releases.Where(
                ReleaseReadRepository.AllUploadsLinkCrypterContainersAndImagesAreComplete
            )
            .Select(release => new
            {
                ReleaseId = release.Id,
                HandledUploadedAt = executionsOfRegistration
                    .Where(execution => execution.ReleaseId == release.Id)
                    .Select(execution => (DateTime?)execution.HandledUploadedAt)
                    .FirstOrDefault()
                    ?? registrationCreatedAt,
            })
            .Select(release => new
            {
                release.ReleaseId,
                NewestUploadedAt = completedUploads
                    .Where(upload => upload.UploadConfig.ReleaseId == release.ReleaseId)
                    .Max(upload => upload.UploadedAt),
                HasUploadNewerThanHandledUploadedAt = completedUploads.Any(upload =>
                    upload.UploadConfig.ReleaseId == release.ReleaseId
                    && upload.UploadedAt > release.HandledUploadedAt
                ),
                IsReupload = completedUploads
                    .Where(upload =>
                        upload.UploadConfig.ReleaseId == release.ReleaseId
                        && upload.UploadedAt > release.HandledUploadedAt
                    )
                    .Any(uploadIsReupload),
            })
            .Where(release => release.HasUploadNewerThanHandledUploadedAt)
            .Where(release =>
                (release.IsReupload && runForReupload)
                || (!release.IsReupload && runForInitialUpload)
            )
            .OrderBy(release => release.ReleaseId)
            .Select(release => new UploadCompletionActionDueRelease(
                ReleaseId: release.ReleaseId,
                IsReupload: release.IsReupload,
                NewestUploadedAt: release.NewestUploadedAt!.Value
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<UploadCompletionActionExecution?> GetExecutionAsync(
        int releaseId,
        int uploadCompletionActionRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.UploadCompletionActionExecutions.SingleOrDefaultAsync(
            execution =>
                execution.ReleaseId == releaseId
                && execution.UploadCompletionActionRegistrationId
                    == uploadCompletionActionRegistrationId,
            cancellationToken
        );
    }

    public void AddExecution(UploadCompletionActionExecution execution)
    {
        dbWrite.UploadCompletionActionExecutions.Add(execution);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Upload> GetCompletedUploads()
    {
        return dbRead
            .UploadConfigs.Where(UploadTargetExpressions.UploadConfigHasActiveHosterRegistration)
            .Where(uploadConfig => uploadConfig.CollectionUploadSlotId == null)
            .SelectMany(uploadConfig => uploadConfig.Uploads)
            .Where(upload =>
                upload.UploadState == UploadState.Completed && upload.UploadedAt != null
            );
    }
}
