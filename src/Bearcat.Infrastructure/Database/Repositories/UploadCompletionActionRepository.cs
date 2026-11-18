using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadCompletionActionRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IUploadCompletionActionRepository
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

        var completedUploads = dbRead
            .UploadConfigs.Where(UploadTargetExpressions.UploadConfigHasActiveHosterRegistration)
            .Where(uploadConfig => uploadConfig.CollectionUploadSlotId == null)
            .SelectMany(uploadConfig => uploadConfig.Uploads)
            .Where(upload =>
                upload.UploadState == UploadState.Completed && upload.UploadedAt != null
            );

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

    public async Task<UploadCompletionActionReleaseDto> GetReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await dbRead
            .Releases.Where(release => release.Id == releaseId)
            .Select(release => new
            {
                release.Name,
                ContentKind = release.ReleaseInfo != null ? release.ReleaseInfo.ContentKind : null,
                ReleaseGroupName = release.ReleaseGroup.Name,
                Nfo = release.ReleaseNfo != null ? release.ReleaseNfo.Content : null,
            })
            .SingleAsync(cancellationToken);

        return new UploadCompletionActionReleaseDto(
            Name: release.Name,
            ContentKind: release.ContentKind?.ToString(),
            ReleaseGroupName: release.ReleaseGroupName,
            Nfo: release.Nfo
        );
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
}
