using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.Search;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchiveReadRepository(IBearcatReadDbContext dbRead) : IArchiveReadRepository
{
    public async Task<ArchiveReadModel?> GetByIdAsync(
        int archiveId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Archives.Where(a => a.Id == archiveId)
            .Select(a => new ArchiveReadModel(
                a.Id,
                a.ArchiveFolderPath,
                a.CreatedAt,
                a.ArchiveFiles.Select(af => new ArchiveReadModel.ArchiveFileReadModel(
                        af.Id,
                        af.FullFileName
                    ))
                    .ToList()
            ))
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<PagedResult<ArchiveListItemReadModel>> SearchArchivesAsync(
        ArchiveSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var pageIndex = Math.Max(0, query.PageIndex);

        var archivesQuery = ApplyArchiveSearch(dbRead.Archives, query);
        var totalCount = await archivesQuery.CountAsync(cancellationToken);

        var archives = await archivesQuery
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(a => new ArchiveListItemReadModel(
                a.Id,
                a.ArchiveConfig.ReleaseId,
                a.ArchiveConfig.Release.Name,
                a.ArchiveConfigId,
                a.ArchiveConfig.Name,
                a.ArchiveConfig.ArchiverName,
                a.ArchiveState,
                a.ArchiveFolderPath,
                a.CreatedAt,
                a.ArchiveFiles.Count,
                a.Uploads.Count,
                a.ErrorMessages
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ArchiveListItemReadModel>(archives, totalCount, pageIndex, pageSize);
    }

    private static IQueryable<Archive> ApplyArchiveSearch(
        IQueryable<Archive> archives,
        ArchiveSearchQuery query
    )
    {
        var searchTerm = SearchTextPatterns.TrimOrNullWhenEmpty(query.SearchTerm);

        if (searchTerm is not null)
        {
            var pattern = SearchTextPatterns.ToLowerCaseContainsPattern(searchTerm);
            var archiveId = SearchTextPatterns.ParseIdWithOptionalHashPrefix(searchTerm);

            archives = archives.Where(a =>
                a.Id == archiveId
                || EF.Functions.Like(a.ArchiveConfig.Release.Name.ToLower(), pattern)
                || EF.Functions.Like(a.ArchiveFolderPath.ToLower(), pattern)
                || a.ArchiveFiles.Any(file =>
                    EF.Functions.Like(file.FullFileName.ToLower(), pattern)
                    || (file.Md5Hash != null && EF.Functions.Like(file.Md5Hash.ToLower(), pattern))
                )
            );
        }

        if (query.ArchiveState is not null)
        {
            archives = archives.Where(a => a.ArchiveState == query.ArchiveState.Value);
        }

        var archiverName = SearchTextPatterns.TrimOrNullWhenEmpty(query.ArchiverName);

        if (archiverName is not null)
        {
            archives = archives.Where(a => a.ArchiveConfig.ArchiverName == archiverName);
        }

        if (query.ReleaseGroupId is not null)
        {
            archives = archives.Where(a =>
                a.ArchiveConfig.Release.ReleaseGroupId == query.ReleaseGroupId.Value
            );
        }

        if (query.OnDiskFilter is not null)
        {
            var archiveStates = ArchiveOnDiskStates.GetArchiveStates(query.OnDiskFilter.Value);
            archives = archives.Where(a => archiveStates.Contains(a.ArchiveState));
        }

        return archives;
    }

    public async Task<
        IReadOnlyList<RunningArchiveReadModel>
    > GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync(
        IReadOnlyList<int> archiveIds,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Archives.Where(a =>
                a.ArchiveState == ArchiveState.Creating
                || a.ArchiveState == ArchiveState.Restoring
                || archiveIds.Contains(a.Id)
            )
            .Select(a => new RunningArchiveReadModel(
                a.Id,
                a.ArchiveConfig.ReleaseId,
                a.ArchiveConfig.Release.Name,
                a.ArchiveConfig.Name,
                a.ArchiveConfig.ArchiverName,
                a.ArchiveState
            ))
            .ToListAsync(cancellationToken);
    }
}
