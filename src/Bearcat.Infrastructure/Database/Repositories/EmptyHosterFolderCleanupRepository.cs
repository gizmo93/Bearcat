using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class EmptyHosterFolderCleanupRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IEmptyHosterFolderCleanupRepository
{
    public async Task<
        IReadOnlyList<HosterRegistration>
    > GetHosterRegistrationsWithEmptyFolderDeletionAsync(CancellationToken cancellationToken)
    {
        return await dbWrite
            .HosterRegistrations.Where(registration =>
                registration.IsActive
                && !registration.HasUnreadableSecrets
                && !registration.RequiresCaptchaVerification
                && registration.DeleteEmptyFolders
            )
            .OrderBy(registration => registration.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetFolderIdsWithoutOnlineFilesAsync(
        int hosterRegistrationId,
        CancellationToken cancellationToken
    )
    {
        return await BuildFolderIdsWithoutOnlineFilesQuery(hosterRegistrationId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsFolderWithoutOnlineFilesAsync(
        int hosterRegistrationId,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        return await BuildFolderIdsWithoutOnlineFilesQuery(hosterRegistrationId)
            .AnyAsync(candidateFolderId => candidateFolderId == folderId, cancellationToken);
    }

    public async Task ClearFolderIdAsync(
        int hosterRegistrationId,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await dbWrite.Database.BeginTransactionAsync(
            cancellationToken
        );

        await dbWrite
            .Uploads.Where(upload =>
                upload.HosterRegistrationId == hosterRegistrationId
                && upload.HosterFolderId == folderId
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(upload => upload.HosterFolderId, (string?)null),
                cancellationToken
            );

        await dbWrite
            .UploadedFiles.Where(file =>
                file.Upload.HosterRegistrationId == hosterRegistrationId
                && file.HosterFolderId == folderId
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(file => file.HosterFolderId, (string?)null),
                cancellationToken
            );

        await transaction.CommitAsync(cancellationToken);
    }

    private IQueryable<string> BuildFolderIdsWithoutOnlineFilesQuery(int hosterRegistrationId)
    {
        List<UploadState> runningUploadStates =
        [
            UploadState.Pending,
            UploadState.Uploading,
            UploadState.CancellationRequested,
        ];

        var uploadsOfRegistration = dbRead.Uploads.Where(upload =>
            upload.HosterRegistrationId == hosterRegistrationId
        );

        return uploadsOfRegistration
            .Where(upload => upload.HosterFolderId != null && upload.HosterFolderId != "")
            .Select(upload => upload.HosterFolderId!)
            .Distinct()
            .Where(folderId =>
                !uploadsOfRegistration.Any(runningUpload =>
                    runningUploadStates.Contains(runningUpload.UploadState)
                    && (
                        runningUpload.HosterFolderId == folderId
                        || runningUpload.UploadedFiles.Any(file => file.HosterFolderId == folderId)
                        || uploadsOfRegistration.Any(folderUpload =>
                            folderUpload.HosterFolderId == folderId
                            && folderUpload.UploadConfig.ReleaseId
                                == runningUpload.UploadConfig.ReleaseId
                        )
                    )
                )
                && !dbRead.UploadedFiles.Any(file =>
                    file.HosterFolderId == folderId
                    && file.OnlineState != OnlineState.Offline
                    && file.Upload.HosterRegistrationId == hosterRegistrationId
                    && file.Upload.UploadState == UploadState.Completed
                    && !dbRead.Uploads.Any(newerUpload =>
                        newerUpload.UploadConfigId == file.Upload.UploadConfigId
                        && newerUpload.Id > file.UploadId
                        && newerUpload.UploadState == UploadState.Completed
                    )
                )
            );
    }
}
