using Bearcat.Domain.UseCases.RunUploadCompletionActions.ReadModels;

namespace Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;

public interface IUploadCompletionActionReleaseStateReadRepository
{
    Task<IReadOnlyList<UploadCompletionActionReleaseStateReadModel>> GetReleaseStatesAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );
}
