using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageUploadConfigs.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class UploadConfigWriteRepository(IBearcatWriteDbContext dbWrite) : IUploadConfigWriteRepository
{
    public async Task<UploadConfig> GetByIdAsync(int uploadConfigId, CancellationToken cancellationToken = default)
    {
        return await dbWrite.UploadConfigs
            .FirstAsync(
                u => u.Id == uploadConfigId,
                cancellationToken: cancellationToken);
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
