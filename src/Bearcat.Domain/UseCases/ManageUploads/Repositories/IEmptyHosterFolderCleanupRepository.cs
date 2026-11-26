using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploads.Repositories;

public interface IEmptyHosterFolderCleanupRepository
{
    Task<IReadOnlyList<HosterRegistration>> GetHosterRegistrationsWithEmptyFolderDeletionAsync(
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<string>> GetFolderIdsWithoutOnlineFilesAsync(
        int hosterRegistrationId,
        CancellationToken cancellationToken
    );

    Task ClearFolderIdAsync(
        int hosterRegistrationId,
        string folderId,
        CancellationToken cancellationToken
    );
}
