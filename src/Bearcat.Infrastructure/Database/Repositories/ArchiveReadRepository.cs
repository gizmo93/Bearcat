using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
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
}
