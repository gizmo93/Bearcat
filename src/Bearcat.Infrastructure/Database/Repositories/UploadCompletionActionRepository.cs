using System.Linq.Expressions;
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
) : IUploadCompletionActionRepository, IUploadCompletionActionStateReadRepository
{
    private static readonly Expression<Func<ReleaseCollection, bool>> AllReleasesPassedQualityGate =
        collection =>
            collection.Releases.All(release =>
                release.QualityGateState == QualityGateState.Passed
                || release.QualityGateState == QualityGateState.ManuallyApproved
            );

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
        var completedUploads = GetCompletedReleaseUploads();

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

    public async Task<UploadCompletionActionDueReleaseCollection?> GetCompletelyUploadedReleaseCollectionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var uploadIsReupload = UploadExpressions.UploadIsReupload(dbRead);
        var completedSlotUploads = GetCompletedCollectionSlotUploads();

        return await dbRead
            .ReleaseCollections.Where(collection => collection.Id == releaseCollectionId)
            .Where(
                ReleaseCollectionRepository.AllSlotUploadsLinkCrypterContainersAndImagesAreComplete(
                    dbRead
                )
            )
            .Select(collection => new
            {
                NewestUploadedAt = completedSlotUploads
                    .Where(upload =>
                        upload.UploadConfig.Release.ReleaseCollectionId == collection.Id
                    )
                    .Max(upload => upload.UploadedAt),
                IsReupload = collection.Releases.All(release =>
                    !completedSlotUploads.Any(upload => upload.UploadConfig.ReleaseId == release.Id)
                    || completedSlotUploads
                        .Where(upload => upload.UploadConfig.ReleaseId == release.Id)
                        .Any(uploadIsReupload)
                ),
            })
            .Where(collection => collection.NewestUploadedAt != null)
            .Select(collection => new UploadCompletionActionDueReleaseCollection(
                ReleaseCollectionId: releaseCollectionId,
                IsReupload: collection.IsReupload,
                NewestUploadedAt: collection.NewestUploadedAt!.Value
            ))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UploadCompletionActionStateReadModel>> GetReleaseStatesAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var completedUploads = GetCompletedReleaseUploads();

        var completelyUploadedReleases = dbRead
            .Releases.Where(release => release.Id == releaseId)
            .Where(ReleaseReadRepository.AllUploadsLinkCrypterContainersAndImagesAreComplete)
            .Where(release =>
                completedUploads.Any(upload => upload.UploadConfig.ReleaseId == release.Id)
            );

        var executionsOfRelease = dbRead.UploadCompletionActionExecutions.Where(execution =>
            execution.ReleaseId == releaseId
        );

        return await GetStatesAsync(
            completelyUploadedEntities: completelyUploadedReleases,
            executions: executionsOfRelease,
            cancellationToken: cancellationToken
        );
    }

    public async Task<
        IReadOnlyList<UploadCompletionActionStateReadModel>
    > GetReleaseCollectionStatesAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var completedSlotUploads = GetCompletedCollectionSlotUploads();

        var completelyUploadedReleaseCollections = dbRead
            .ReleaseCollections.Where(collection => collection.Id == releaseCollectionId)
            .Where(
                ReleaseCollectionRepository.AllSlotUploadsLinkCrypterContainersAndImagesAreComplete(
                    dbRead
                )
            )
            .Where(collection =>
                completedSlotUploads.Any(upload =>
                    upload.UploadConfig.Release.ReleaseCollectionId == collection.Id
                )
            );

        var executionsOfReleaseCollection = dbRead.UploadCompletionActionExecutions.Where(
            execution => execution.ReleaseCollectionId == releaseCollectionId
        );

        return await GetStatesAsync(
            completelyUploadedEntities: completelyUploadedReleaseCollections,
            executions: executionsOfReleaseCollection,
            cancellationToken: cancellationToken
        );
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

        var completedUploads = GetCompletedReleaseUploads();

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

    public async Task<
        IReadOnlyList<UploadCompletionActionDueReleaseCollection>
    > GetDueReleaseCollectionsAsync(
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

        var completedSlotUploads = GetCompletedCollectionSlotUploads();

        return await dbRead
            .ReleaseCollections.Where(
                ReleaseCollectionRepository.AllSlotUploadsLinkCrypterContainersAndImagesAreComplete(
                    dbRead
                )
            )
            .Where(AllReleasesPassedQualityGate)
            .Select(collection => new
            {
                ReleaseCollectionId = collection.Id,
                HandledUploadedAt = executionsOfRegistration
                    .Where(execution => execution.ReleaseCollectionId == collection.Id)
                    .Select(execution => (DateTime?)execution.HandledUploadedAt)
                    .FirstOrDefault()
                    ?? registrationCreatedAt,
            })
            .Select(collection => new
            {
                collection.ReleaseCollectionId,
                NewestUploadedAt = completedSlotUploads
                    .Where(upload =>
                        upload.UploadConfig.Release.ReleaseCollectionId
                        == collection.ReleaseCollectionId
                    )
                    .Max(upload => upload.UploadedAt),
                HasUploadNewerThanHandledUploadedAt = completedSlotUploads.Any(upload =>
                    upload.UploadConfig.Release.ReleaseCollectionId
                        == collection.ReleaseCollectionId
                    && upload.UploadedAt > collection.HandledUploadedAt
                ),
                IsReupload = completedSlotUploads
                    .Where(upload =>
                        upload.UploadConfig.Release.ReleaseCollectionId
                            == collection.ReleaseCollectionId
                        && upload.UploadedAt > collection.HandledUploadedAt
                    )
                    .All(uploadIsReupload),
            })
            .Where(collection => collection.HasUploadNewerThanHandledUploadedAt)
            .Where(collection =>
                (collection.IsReupload && runForReupload)
                || (!collection.IsReupload && runForInitialUpload)
            )
            .OrderBy(collection => collection.ReleaseCollectionId)
            .Select(collection => new UploadCompletionActionDueReleaseCollection(
                ReleaseCollectionId: collection.ReleaseCollectionId,
                IsReupload: collection.IsReupload,
                NewestUploadedAt: collection.NewestUploadedAt!.Value
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<UploadCompletionActionExecution?> GetReleaseExecutionAsync(
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

    public async Task<UploadCompletionActionExecution?> GetReleaseCollectionExecutionAsync(
        int releaseCollectionId,
        int uploadCompletionActionRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.UploadCompletionActionExecutions.SingleOrDefaultAsync(
            execution =>
                execution.ReleaseCollectionId == releaseCollectionId
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

    private async Task<IReadOnlyList<UploadCompletionActionStateReadModel>> GetStatesAsync<TEntity>(
        IQueryable<TEntity> completelyUploadedEntities,
        IQueryable<UploadCompletionActionExecution> executions,
        CancellationToken cancellationToken
    )
    {
        return await dbRead
            .UploadCompletionActionRegistrations.Where(registration =>
                registration.IsActive && !registration.HasUnreadableSecrets
            )
            .LeftJoin(
                executions,
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
            .Select(state => new UploadCompletionActionStateReadModel(
                RegistrationId: state.RegistrationId,
                RegistrationName: state.RegistrationName,
                IsCompletelyUploaded: completelyUploadedEntities.Any(),
                ExecutedAt: state.ExecutedAt,
                ResultText: state.ResultText,
                ErrorMessage: state.ErrorMessage
            ))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Upload> GetCompletedReleaseUploads()
    {
        return GetCompletedUploads(
            dbRead.UploadConfigs.Where(uploadConfig => uploadConfig.CollectionUploadSlotId == null)
        );
    }

    private IQueryable<Upload> GetCompletedCollectionSlotUploads()
    {
        return GetCompletedUploads(
            dbRead.UploadConfigs.Where(uploadConfig => uploadConfig.CollectionUploadSlotId != null)
        );
    }

    private static IQueryable<Upload> GetCompletedUploads(IQueryable<UploadConfig> uploadConfigs)
    {
        return uploadConfigs
            .Where(UploadTargetExpressions.UploadConfigHasActiveHosterRegistration)
            .SelectMany(uploadConfig => uploadConfig.Uploads)
            .Where(upload =>
                upload.UploadState == UploadState.Completed && upload.UploadedAt != null
            );
    }
}
