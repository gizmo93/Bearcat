using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
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
