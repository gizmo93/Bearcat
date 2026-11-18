using Bearcat.Domain.UseCases.RunUploadCompletionActions.ReadModels;

namespace Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;

public interface IUploadCompletionActionStateReadRepository
{
    Task<IReadOnlyList<UploadCompletionActionStateReadModel>> GetReleaseStatesAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<UploadCompletionActionStateReadModel>> GetReleaseCollectionStatesAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );
}
