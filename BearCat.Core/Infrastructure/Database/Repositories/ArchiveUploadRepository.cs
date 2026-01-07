using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class ArchiveUploadRepository(IBearcatWriteDbContext dbWrite, IBearcatReadDbContext dbRead)
{
    public async Task<IReadOnlyList<int>> GetArchiveIdsToCheckOnlineStatusAsync(CancellationToken cancellationToken)
    {
        return await dbRead.ArchiveUploads
            .Select(ai => ai.Id)
            .ToListAsync(cancellationToken);
    }
}
