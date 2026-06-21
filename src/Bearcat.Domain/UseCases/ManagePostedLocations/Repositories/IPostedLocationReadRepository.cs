using Bearcat.Domain.UseCases.ManagePostedLocations.ReadModels;

namespace Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;

public interface IPostedLocationReadRepository
{
    Task<IReadOnlyList<PostedLocationReadModel>> GetForReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<PostedLocationReadModel>> GetForCollectionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );
}
