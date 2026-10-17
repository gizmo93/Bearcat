using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadConfigLinkCrypterWriteRepository(IBearcatWriteDbContext dbWrite)
    : IUploadConfigLinkCrypterWriteRepository
{
    public async Task<UploadConfigLinkCrypter> GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.UploadConfigLinkCrypters.FirstAsync(
            u => u.Id == id,
            cancellationToken
        );
    }

    public async Task<UploadSource> GetUploadSourceOfUploadConfigAsync(
        int uploadConfigId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .UploadConfigs.Where(uploadConfig => uploadConfig.Id == uploadConfigId)
            .Select(uploadConfig => uploadConfig.UploadSource)
            .FirstAsync(cancellationToken);
    }

    public void Add(UploadConfigLinkCrypter uploadConfigLinkCrypter)
    {
        dbWrite.UploadConfigLinkCrypters.Add(uploadConfigLinkCrypter);
    }

    public void Remove(UploadConfigLinkCrypter uploadConfigLinkCrypter)
    {
        dbWrite.UploadConfigLinkCrypters.Remove(uploadConfigLinkCrypter);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
