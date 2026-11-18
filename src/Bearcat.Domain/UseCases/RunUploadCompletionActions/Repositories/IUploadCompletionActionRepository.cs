using Bearcat.Abstractions.UploadCompletionAction.Dto;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Models;

namespace Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;

public interface IUploadCompletionActionRepository
{
    Task<
        IReadOnlyList<UploadCompletionActionRegistration>
    > GetActiveRegistrationsWithReadableSecretsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadCompletionActionDueRelease>> GetDueReleasesAsync(
        UploadCompletionActionRegistration registration,
        CancellationToken cancellationToken = default
    );

    Task<UploadCompletionActionReleaseDto> GetReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<UploadCompletionActionExecution?> GetExecutionAsync(
        int releaseId,
        int uploadCompletionActionRegistrationId,
        CancellationToken cancellationToken = default
    );

    void AddExecution(UploadCompletionActionExecution execution);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
