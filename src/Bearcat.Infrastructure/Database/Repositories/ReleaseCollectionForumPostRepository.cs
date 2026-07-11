using Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseCollectionForumPostRepository(IBearcatReadDbContext dbRead)
    : IReleaseCollectionForumPostRepository
{
    public async Task<CollectionForumPostReadModel?> GetAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await dbRead
            .ReleaseCollections.Where(collection => collection.Id == releaseCollectionId)
            .Select(collection => new
            {
                collection.Name,
                collection.Key,
                collection.PrimaryLanguageCode,
                ReleaseGroupName = collection.ReleaseGroup.Name,
                Series = collection.Metadata == null
                    ? null
                    : new CollectionForumPostSeriesReadModel(
                        collection.Metadata.Title,
                        collection.Metadata.Description,
                        collection.Metadata.CoverUrl,
                        collection.Metadata.MetadataDatabaseUrl
                    ),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (collection is null)
        {
            return null;
        }

        var releases = await dbRead
            .Releases.Where(release => release.ReleaseCollectionId == releaseCollectionId)
            .OrderBy(release => release.Name)
            .ThenBy(release => release.Id)
            .Select(release => new CollectionForumPostReleaseReadModel(release.Id, release.Name))
            .ToListAsync(cancellationToken);

        return new CollectionForumPostReadModel(
            Name: collection.Name,
            Key: collection.Key,
            ReleaseGroupName: collection.ReleaseGroupName,
            PrimaryLanguageCode: collection.PrimaryLanguageCode,
            Series: collection.Series,
            Releases: releases
        );
    }
}
