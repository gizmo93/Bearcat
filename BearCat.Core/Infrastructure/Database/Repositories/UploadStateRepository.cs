using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageUploads.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class UploadStateRepository(IBearcatWriteDbContext dbWrite)
    : IUploadStateRepository
{
    public async Task<IReadOnlyList<Upload>> GetUploadsToCheckAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        List<OnlineState> onlineStatesToCheck =
        [
            OnlineState.Online,
            OnlineState.PartiallyOnline
        ];
        
        var lastCheckThreshold = utcNow.AddMinutes(-30);

        return await dbWrite.Uploads
            .AsSplitQuery()
            .Include(u => u.UploadConfig)
            .ThenInclude(uc => uc.HosterRegistration)
            .Include(u => u.UploadedFiles)
            .Where(u => 
                onlineStatesToCheck.Contains(u.OnlineState)
                && u.UploadedFiles.Any(f => f.CheckedAt == null
                                            || f.CheckedAt < lastCheckThreshold))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.UploadConfigs
            .Where(u => !u.Uploads.Any())
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public void Add(Upload upload)
    {
        dbWrite.Add(upload);
    }
}
