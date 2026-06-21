using Bearcat.Domain.UseCases.ManagePostedLocations.ReadModels;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class PostedLocationReadRepository(IBearcatReadDbContext dbRead)
    : IPostedLocationReadRepository
{
    public async Task<IReadOnlyList<PostedLocationReadModel>> GetForReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .PostedLocations.Where(location => location.ReleaseId == releaseId)
            .OrderBy(location => location.Id)
            .Select(location => new PostedLocationReadModel(
                location.Id,
                location.Url,
                location.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PostedLocationReadModel>> GetForCollectionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .PostedLocations.Where(location => location.ReleaseCollectionId == releaseCollectionId)
            .OrderBy(location => location.Id)
            .Select(location => new PostedLocationReadModel(
                location.Id,
                location.Url,
                location.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }
}
