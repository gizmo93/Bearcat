using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadConfigWriteRepository(IBearcatWriteDbContext dbWrite)
    : IUploadConfigWriteRepository
{
    public async Task<UploadConfig> GetByIdAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.UploadConfigs.FirstAsync(
            u => u.Id == uploadConfigId,
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyList<Upload>> GetWaitingOrPendingUploadsWithoutUploadedFilesAsync(
        int uploadConfigId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Uploads.Where(u =>
                u.UploadConfigId == uploadConfigId
                && (
                    u.UploadState == UploadState.WaitingForArchive
                    || u.UploadState == UploadState.Pending
                )
                && !u.UploadedFiles.Any()
            )
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public void Add(UploadConfig uploadConfig)
    {
        dbWrite.Add(uploadConfig);
    }

    public void Remove(UploadConfig uploadConfig)
    {
        dbWrite.Remove(uploadConfig);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
