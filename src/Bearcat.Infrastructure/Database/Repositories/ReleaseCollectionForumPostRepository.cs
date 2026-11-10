using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
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

        var releaseIds = await dbRead
            .Releases.Where(release => release.ReleaseCollectionId == releaseCollectionId)
            .OrderBy(release => release.Name)
            .ThenBy(release => release.Id)
            .Select(release => release.Id)
            .ToListAsync(cancellationToken);

        var uploadSlots = await dbRead
            .CollectionUploadSlots.Where(slot => slot.ReleaseCollectionId == releaseCollectionId)
            .OrderBy(slot => slot.Name)
            .ThenBy(slot => slot.Id)
            .Select(slot => new CollectionForumPostUploadSlotReadModel(
                slot.Name,
                slot.Key,
                dbRead
                    .LinkCrypterContainers.Where(container =>
                        container.CollectionUploadSlotId == slot.Id
                        && container.Scope == LinkCrypterContainerScope.ReleaseCollection
                        && container.ContainerUrl != string.Empty
                    )
                    .OrderBy(container => container.LinkCrypterRegistration.Name)
                    .ThenBy(container => container.Id)
                    .Select(container => new ReleaseForumPostLinkCrypterReadModel(
                        container.LinkCrypterRegistration.Name,
                        container.Password,
                        container.ContainerUrl,
                        container.StatusImageId,
                        container.CreatedAt
                    ))
                    .ToList()
            ))
            .ToListAsync(cancellationToken);

        return new CollectionForumPostReadModel(
            Name: collection.Name,
            Key: collection.Key,
            ReleaseGroupName: collection.ReleaseGroupName,
            PrimaryLanguageCode: collection.PrimaryLanguageCode,
            Series: collection.Series,
            ReleaseIds: releaseIds,
            UploadSlots: uploadSlots
        );
    }
}
