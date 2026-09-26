using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManagePostedLocations.Dto;
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

    Task<PagedResult<PostedLocationReadModel>> SearchForReleaseAsync(
        ReleasePostedLocationSearchQuery query,
        CancellationToken cancellationToken = default
    );

    Task<PostedLocationReadModel?> GetByIdForReleaseAsync(
        int releaseId,
        int postedLocationId,
        CancellationToken cancellationToken = default
    );
}
