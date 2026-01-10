using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageArchives.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class ArchiveCreationRepository(IBearcatWriteDbContext dbWrite)
    : IArchiveCreationRepository
{
    public async Task<IReadOnlyList<Upload>> GetUploadsWithoutArchiveAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.Uploads
            .AsSplitQuery()
            .Include(u => u.UploadConfig)
            .ThenInclude(uc => uc.Uploads)
            .Include(u => u.UploadConfig)
            .ThenInclude(u => u.ArchiveConfig)
            .ThenInclude(ac => ac.Archives)
            .Include(u => u.UploadConfig)
            .ThenInclude(uc => uc.Release)
            .Where(u => u.ArchiveId == null || u.UploadState == UploadState.WaitingForArchive)
            .ToListAsync(cancellationToken: cancellationToken);

    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public void Add(Archive archive)
    {
        dbWrite.Add(archive);
    }
}
