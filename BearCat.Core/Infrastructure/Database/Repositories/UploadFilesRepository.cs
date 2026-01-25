using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageUploads.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class UploadFilesRepository(IBearcatWriteDbContext dbWrite)
    : IUploadFilesRepository
{
    public async Task<IReadOnlyList<Upload>> GetPendingUploadsAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.Uploads
            .AsSplitQuery()
            .Include(u => u.UploadedFiles)
            .Include(u => u.UploadConfig)
            .ThenInclude(uc => uc.HosterRegistration)
            .Include(u => u.Archive)
            .ThenInclude(a => a!.ArchiveFiles)
            .Where(u => u.UploadState == UploadState.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
