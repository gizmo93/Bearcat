using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManagePostedLocations.Dto;
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
            .Select(ToPostedLocationReadModel())
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
            .Select(ToPostedLocationReadModel())
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<PostedLocationReadModel>> SearchForReleaseAsync(
        ReleasePostedLocationSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);

        var postedLocationsQuery = dbRead.PostedLocations.Where(location =>
            location.ReleaseId == query.ReleaseId
        );

        var totalCount = await postedLocationsQuery.CountAsync(cancellationToken);

        var postedLocations = await postedLocationsQuery
            .OrderBy(location => location.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(ToPostedLocationReadModel())
            .ToListAsync(cancellationToken);

        return new PagedResult<PostedLocationReadModel>(
            postedLocations,
            totalCount,
            pageIndex,
            pageSize
        );
    }

    public async Task<PostedLocationReadModel?> GetByIdForReleaseAsync(
        int releaseId,
        int postedLocationId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .PostedLocations.Where(location =>
                location.Id == postedLocationId && location.ReleaseId == releaseId
            )
            .Select(ToPostedLocationReadModel())
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static Expression<
        Func<PostedLocation, PostedLocationReadModel>
    > ToPostedLocationReadModel()
    {
        return location => new PostedLocationReadModel(
            location.Id,
            location.DistributionSiteRegistrationId,
            location.DistributionSiteRegistration != null
                ? location.DistributionSiteRegistration.Name
                : null,
            location.ForumPostTemplateId,
            location.Url,
            location.CreatedAt,
            location.ContentUpdatedAt
        );
    }
}
