using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;

public interface IPostedLocationWriteRepository
{
    Task<PostedLocation> GetByIdAsync(
        int postedLocationId,
        CancellationToken cancellationToken = default
    );

    Task<int?> FindReleasePostedLocationIdByUrlAsync(
        int releaseId,
        string url,
        CancellationToken cancellationToken = default
    );

    void Add(PostedLocation postedLocation);

    void Remove(PostedLocation postedLocation);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
