using Bearcat.Domain.UseCases.ManageForumPostTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ForumPostTemplatePreviewEntityReadRepository(IBearcatReadDbContext dbRead)
    : IForumPostTemplatePreviewEntityReadRepository
{
    public async Task<IReadOnlyList<ForumPostTemplatePreviewEntityReadModel>> SearchAsync(
        ForumPostTemplateType type,
        string? searchTerm,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        var trimmedSearchTerm = SearchTextPatterns.TrimOrNullWhenEmpty(searchTerm);
        var pattern = trimmedSearchTerm is null
            ? null
            : SearchTextPatterns.ToLowerCaseContainsPattern(trimmedSearchTerm);

        return type switch
        {
            ForumPostTemplateType.Release => await SearchReleasesAsync(
                pattern,
                limit,
                cancellationToken
            ),
            ForumPostTemplateType.ReleaseCollection => await SearchReleaseCollectionsAsync(
                pattern,
                limit,
                cancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    public async Task<ForumPostTemplatePreviewEntityReadModel?> GetAsync(
        ForumPostTemplateType type,
        int entityId,
        CancellationToken cancellationToken = default
    )
    {
        return type switch
        {
            ForumPostTemplateType.Release => await dbRead
                .Releases.Where(release => release.Id == entityId)
                .Select(release => new ForumPostTemplatePreviewEntityReadModel(
                    release.Id,
                    release.Name
                ))
                .FirstOrDefaultAsync(cancellationToken),
            ForumPostTemplateType.ReleaseCollection => await dbRead
                .ReleaseCollections.Where(collection => collection.Id == entityId)
                .Select(collection => new ForumPostTemplatePreviewEntityReadModel(
                    collection.Id,
                    collection.Name
                ))
                .FirstOrDefaultAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private async Task<List<ForumPostTemplatePreviewEntityReadModel>> SearchReleasesAsync(
        string? pattern,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var releases = dbRead.Releases.AsQueryable();

        if (pattern is not null)
        {
            releases = releases.Where(release =>
                EF.Functions.Like(release.Name.ToLower(), pattern)
            );
        }

        return await releases
            .OrderByDescending(release => release.CreatedAt)
            .ThenByDescending(release => release.Id)
            .Take(limit)
            .Select(release => new ForumPostTemplatePreviewEntityReadModel(
                release.Id,
                release.Name
            ))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<ForumPostTemplatePreviewEntityReadModel>> SearchReleaseCollectionsAsync(
        string? pattern,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var collections = dbRead.ReleaseCollections.AsQueryable();

        if (pattern is not null)
        {
            collections = collections.Where(collection =>
                EF.Functions.Like(collection.Name.ToLower(), pattern)
            );
        }

        return await collections
            .OrderByDescending(collection => collection.CreatedAt)
            .ThenByDescending(collection => collection.Id)
            .Take(limit)
            .Select(collection => new ForumPostTemplatePreviewEntityReadModel(
                collection.Id,
                collection.Name
            ))
            .ToListAsync(cancellationToken);
    }
}
