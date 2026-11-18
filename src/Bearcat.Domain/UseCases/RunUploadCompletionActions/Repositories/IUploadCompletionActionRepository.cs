using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;

namespace Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;

public interface IUploadCompletionActionRepository
{
    Task<
        IReadOnlyList<UploadCompletionActionRegistration>
    > GetActiveRegistrationsWithReadableSecretsAsync(CancellationToken cancellationToken = default);

    Task<UploadCompletionActionRegistration> GetRegistrationAsync(
        int id,
        CancellationToken cancellationToken = default
    );

    Task<UploadCompletionActionDueRelease?> GetCompletelyUploadedReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<UploadCompletionActionDueReleaseCollection?> GetCompletelyUploadedReleaseCollectionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<UploadCompletionActionDueRelease>> GetDueReleasesAsync(
        UploadCompletionActionRegistration registration,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<UploadCompletionActionDueReleaseCollection>> GetDueReleaseCollectionsAsync(
        UploadCompletionActionRegistration registration,
        CancellationToken cancellationToken = default
    );

    Task<UploadCompletionActionExecution?> GetReleaseExecutionAsync(
        int releaseId,
        int uploadCompletionActionRegistrationId,
        CancellationToken cancellationToken = default
    );

    Task<UploadCompletionActionExecution?> GetReleaseCollectionExecutionAsync(
        int releaseCollectionId,
        int uploadCompletionActionRegistrationId,
        CancellationToken cancellationToken = default
    );

    void AddExecution(UploadCompletionActionExecution execution);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
